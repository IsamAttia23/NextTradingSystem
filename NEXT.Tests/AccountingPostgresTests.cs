using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using Xunit;

namespace NEXT.Tests;

public sealed class AccountingPostgresTests
{
    [PostgresFact]
    public async Task ConcurrentRetriesWriteOneBalancedEntryAndRawSqlCannotChangeOrExtendIt()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection,
            configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        await using (var scope = app.Services.CreateAsyncScope()) await AccountingFoundationTests.Activate(scope.ServiceProvider, actor);
        var input = AccountingFoundationTests.TreasuryOpening(100);
        async Task<Guid> Post()
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAccountingPostingService>().PostAsync(input, default);
        }
        var results = await Task.WhenAll(Post(), Post(), Post());
        Assert.Single(results.Distinct());
        var entryId = results[0];
        Assert.Equal(1, await f.Db.LedgerEntries.CountAsync());
        Assert.Equal(2, await f.Db.AccountTransactions.CountAsync());
        foreach (var sql in new[]
        {
            "UPDATE \"Accounts\" SET \"Name\" = 'Changed'",
            "DELETE FROM \"Accounts\"",
            "UPDATE \"LedgerEntries\" SET \"Description\" = 'Changed'",
            "DELETE FROM \"LedgerEntries\"",
            "UPDATE \"AccountTransactions\" SET \"Debit\" = 9",
            "DELETE FROM \"AccountTransactions\""
        })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => f.Db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal("23514", error.SqlState);
        }
        var lineId = Guid.NewGuid();
        var accountId = Account.IdFor(AccountType.Treasury);
        var append = await Assert.ThrowsAsync<PostgresException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AccountTransactions" ("Id", "LedgerEntryId", "Sequence", "AccountId", "Debit", "Credit")
            VALUES ({lineId}, {entryId}, 3, {accountId}, 1, 0)
            """));
        Assert.Equal("23514", append.SqlState);
        Assert.Equal(100m, await f.Db.AccountTransactions.SumAsync(line => line.Debit));
        await using var readsScope = app.Services.CreateAsyncScope();
        var balances = await readsScope.ServiceProvider.GetRequiredService<IAccountingReadService>().GetAccountsAsync(null, null, default);
        Assert.Equal(100m, balances.Single(item => item.Type == AccountType.Treasury).Balance);
    }

    [PostgresFact]
    public async Task FailedFinalizationRollsBackTheEntryLinesPaymentAndAuditRows()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection,
            configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        await using var scope = app.Services.CreateAsyncScope();
        var db = await AccountingFoundationTests.Activate(scope.ServiceProvider, actor);
        var before = await f.Db.AuditLogs.CountAsync();
        await f.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fail_accounting_post() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected posting failure'; END $$;
            CREATE TRIGGER test_fail_post BEFORE UPDATE ON "LedgerEntries"
            FOR EACH ROW EXECUTE FUNCTION fail_accounting_post();
            """);
        var payment = new Payment { PaymentNumber = "ROLLBACK-ACCOUNTING", Amount = 10,
            PaymentType = PaymentType.Receipt, PaymentDate = new(2026, 11, 21) };
        var input = new PostingRequestDto(Guid.NewGuid(), LedgerEntryKind.Receipt, AccountingReferenceType.Payment,
            payment.Id = Guid.NewGuid(), new(2026, 11, 21), "Receipt",
            [new(AccountType.Treasury, 10, 0), new(AccountType.CustomerAdvances, 0, 10)], PaymentId: payment.Id);
        var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var posting = scope.ServiceProvider.GetRequiredService<IAccountingPostingService>();
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => unit.ExecuteReadCommittedAsync(async ct =>
        {
            db.Payments.Add(payment);
            await unit.SaveChangesAsync(ct);
            return await posting.PostAsync(input, ct);
        }, default));
        Assert.Contains("Injected posting failure", error.InnerException!.Message);
        Assert.Empty(await f.Db.LedgerEntries.ToListAsync());
        Assert.Empty(await f.Db.AccountTransactions.ToListAsync());
        Assert.Empty(await f.Db.Payments.ToListAsync());
        Assert.Equal(before, await f.Db.AuditLogs.CountAsync());
    }

    [PostgresFact]
    public async Task CompetingDebtReceiptsRejectTheExcessAndKeepTheBalanceCorrect()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection,
            configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        var customer = new Customer { Name = "Debt owner", Phone = "+249123456789" };
        f.Db.Customers.Add(customer);
        await f.Db.SaveChangesAsync();
        await using (var scope = app.Services.CreateAsyncScope()) await AccountingFoundationTests.Activate(scope.ServiceProvider, actor);
        var account = CustomerDebtAccount.Create(customer.Id, actor.UserId!.Value, DateTimeOffset.UtcNow);
        var item = DebtItem.Create(account.Id, DebtItemKind.OpeningBalance, null, 100, new(2026, 11, 21), "Opening debt", actor.UserId.Value);
        f.Db.AddRange(account, item);
        await f.Db.SaveChangesAsync();
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IAccountingPostingService>().PostAsync(new(Guid.NewGuid(),
                LedgerEntryKind.OpeningBalance, AccountingReferenceType.DebtOpening, item.Id, new(2026, 11, 21), "Opening debt",
                [new(AccountType.Receivables, 100, 0, customer.Id, item.Id), new(AccountType.OpeningBalances, 0, 100)]), default);
        async Task<bool> Pay()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NEXT.Infrastructure.Persistence.NEXTDbContext>();
            var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            try
            {
                await unit.ExecuteReadCommittedAsync(async ct =>
                {
                    var payment = new Payment { Id = Guid.NewGuid(), PaymentNumber = "DEBT-" + Guid.NewGuid().ToString("N"),
                        Amount = 70, CustomerId = customer.Id, PaymentType = PaymentType.Receipt,
                        Purpose = PaymentPurpose.DebtReceipt, PaymentDate = new(2026, 11, 21) };
                    db.Payments.Add(payment);
                    await unit.SaveChangesAsync(ct);
                    return await scope.ServiceProvider.GetRequiredService<IAccountingPostingService>().PostAsync(new(Guid.NewGuid(),
                        LedgerEntryKind.Receipt, AccountingReferenceType.Payment, payment.Id, payment.PaymentDate, "Debt receipt",
                        [new(AccountType.Treasury, 70, 0, customer.Id), new(AccountType.Receivables, 0, 70, customer.Id, item.Id)],
                        PaymentId: payment.Id), ct);
                }, default);
                return true;
            }
            catch (ConflictException error) when (error.Message == "AccountingOverpayment") { return false; }
        }
        var results = await Task.WhenAll(Pay(), Pay());
        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
        Assert.Equal(1, await f.Db.Payments.CountAsync());
        Assert.Equal(2, await f.Db.LedgerEntries.CountAsync());
        Assert.Equal(30m, await f.Db.AccountTransactions.Where(line => line.DebtItemId == item.Id).SumAsync(line => line.Debit - line.Credit));
    }

    [PostgresFact]
    public async Task DatabaseRejectsUnbalancedAndUnfinalizedEntriesAndMigrationCanBeReapplied()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new NEXT.Infrastructure.Identity.Entities.ApplicationUser
            { Id = Guid.NewGuid(), UserName = "raw-ledger", FullName = "Raw operator", BranchId = f.Branch };
        f.Db.Users.Add(actor);
        await f.Db.SaveChangesAsync();
        foreach (var finalize in new[] { false, true })
        {
            async Task InvalidCommit()
            {
                await using var transaction = await f.Db.Database.BeginTransactionAsync();
                var id = Guid.NewGuid(); var operation = Guid.NewGuid(); var reference = Guid.NewGuid();
                var key = LedgerEntry.Key(LedgerEntryKind.OpeningBalance, AccountingReferenceType.Adjustment, reference);
                var now = DateTimeOffset.UtcNow; var date = new DateOnly(2026, 11, 21);
                await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "LedgerEntries" ("Id", "OperationId", "PostingKey", "RequestHash", "Kind", "ReferenceType",
                        "ReferenceId", "EntryDate", "Description", "ActorId", "ActorName", "CreatedAt")
                    VALUES ({id}, {operation}, {key}, {new string('A', 64)}, 3, 4, {reference}, {date}, 'Invalid entry', {actor.Id}, 'Raw operator', {now})
                    """);
                var line = Guid.NewGuid(); var cash = Account.IdFor(AccountType.Treasury);
                await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "AccountTransactions" ("Id", "LedgerEntryId", "Sequence", "AccountId", "Debit", "Credit")
                    VALUES ({line}, {id}, 1, {cash}, 10, 0)
                    """);
                if (finalize) await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"LedgerEntries\" SET \"PostedAt\" = {now} WHERE \"Id\" = {id}");
                await transaction.CommitAsync();
            }
            var error = await Assert.ThrowsAsync<PostgresException>(InvalidCommit);
            Assert.Equal("23514", error.SqlState);
            Assert.Empty(await f.Db.LedgerEntries.ToListAsync());
        }
        await f.Db.GetService<IMigrator>().MigrateAsync("20261121093105_RemoveAccountingSystem");
        await f.Db.Database.MigrateAsync();
        Assert.Equal(12, await f.Db.Accounts.CountAsync());
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }
}
