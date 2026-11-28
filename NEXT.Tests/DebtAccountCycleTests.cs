using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Features.Accounting;
using NEXT.Application.Features.Invoices;
using NEXT.Domain.Enums;
using Npgsql;
using Xunit;

namespace NEXT.Tests;

public sealed class DebtAccountCycleTests
{
    [PostgresFact]
    public async Task MigrationBackfillsExistingInvoicesAndClosesOnlySettledAccountsWithoutChangingMoney()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection,
            configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        app.Configuration["Inventory:AllowInvoiceOverselling"] = "true";
        Guid settledId, openId;
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            var db = await AccountingFoundationTests.Activate(sp, actor);
            var settledCustomer = await DebtBookTests.Prepare(db, fixture.Source, fixture.Product);
            await DebtBookTests.Sell(sp, db, settledCustomer, fixture.Product, 2, InvoiceStatus.Delivered);
            var account = await db.CustomerDebtAccounts.AsNoTracking().SingleAsync();
            settledId = account.Id;
            await sp.GetRequiredService<DebtBookPayments>().ReceiveAsync(account.Id, new()
            {
                OperationId = Guid.NewGuid(), RowVersion = account.RowVersion, Amount = 20,
                PaymentMethod = PaymentMethod.Cash, CashAccount = AccountType.Treasury,
                Notes = "Payment before the account-cycle migration"
            }, default);
            var customer = new NEXT.Domain.Entities.Customer { Name = "Unsettled legacy customer", Phone = "+249123456780" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            await DebtBookTests.Sell(sp, db, customer, fixture.Product, 3, InvoiceStatus.Shipped);
            openId = await db.CustomerDebtAccounts.Where(a => a.CustomerId == customer.Id).Select(a => a.Id).SingleAsync();
        }
        finally { await app.DisposeAsync(); }
        var entries = await fixture.Db.LedgerEntries.CountAsync();
        var paid = await fixture.Db.Payments.SumAsync(p => p.Amount);
        var migrator = fixture.Db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261126091925_AddCustomerDebtBook");
        await migrator.MigrateAsync();
        Assert.Equal(entries, await fixture.Db.LedgerEntries.CountAsync());
        Assert.Equal(paid, await fixture.Db.Payments.SumAsync(p => p.Amount));
        Assert.Equal(50, await fixture.Db.Invoices.SumAsync(i => i.TotalAmount));
        Assert.Equal(30, await fixture.Db.Invoices.SumAsync(i => i.RemainingAmount));
        var accounts = await fixture.Db.CustomerDebtAccounts.AsNoTracking().ToListAsync();
        Assert.NotNull(accounts.Single(a => a.Id == settledId).ClosedAt);
        Assert.Null(accounts.Single(a => a.Id == openId).ClosedAt);
        Assert.All(accounts, a => Assert.Equal(1, a.CycleNumber));
        Assert.Equal(1, await fixture.Db.Invoices.CountAsync(i => i.DebtAccountId == settledId));
        Assert.Equal(1, await fixture.Db.Invoices.CountAsync(i => i.DebtAccountId == openId));
        Assert.Equal("Payment before the account-cycle migration", (await fixture.Db.DebtAccountPayments.SingleAsync()).Notes);
    }

    [PostgresFact]
    public async Task SettledAccountClosesAndNewWithdrawalStartsAnIndependentCycle()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection,
            configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        app.Configuration["Inventory:AllowInvoiceOverselling"] = "true";
        await using var scope = app.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = await AccountingFoundationTests.Activate(sp, actor);
        var customer = await DebtBookTests.Prepare(db, fixture.Source, fixture.Product);
        var invoice = await DebtBookTests.Sell(sp, db, customer, fixture.Product, 2, InvoiceStatus.Delivered);
        var reads = sp.GetRequiredService<DebtBookReads>();
        var first = await db.CustomerDebtAccounts.AsNoTracking().SingleAsync();
        await sp.GetRequiredService<DebtBookPayments>().ReceiveAsync(first.Id, new()
        {
            OperationId = Guid.NewGuid(), RowVersion = first.RowVersion, Amount = 20,
            PaymentMethod = PaymentMethod.Cash, CashAccount = AccountType.Treasury,
            Notes = "20 SDG: payment for previously withdrawn goods"
        }, default);
        var settled = await reads.StatementAsync(first.Id, default);
        Assert.NotNull(settled.Account.ClosedAt);
        Assert.Equal(1, settled.Account.CycleNumber);
        Assert.Equal(0, settled.Balance);
        Assert.Equal(20, settled.Paid);
        Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE \"CustomerDebtAccounts\" SET \"ClosedAt\"=NULL,\"ClosedBy\"=NULL"))).SqlState);

        var input = new DebtBookWithdrawalInput
        {
            OperationId = Guid.NewGuid(), RowVersion = settled.Account.RowVersion,
            CustomerId = customer.Id, CustomerName = customer.Name, CustomerPhone = customer.Phone,
            Lines = [new() { ProductId = fixture.Product, Quantity = 3, QuotedUnitPrice = 10 }]
        };
        var withdrawals = sp.GetRequiredService<DebtBookWithdrawals>();
        var nextInvoice = await withdrawals.WithdrawAsync(first.Id, input, default);
        Assert.Equal(nextInvoice, await withdrawals.WithdrawAsync(first.Id, input, default));
        var secondId = await reads.AccountForInvoiceAsync(nextInvoice, default);
        Assert.NotEqual(first.Id, secondId);
        var second = await reads.StatementAsync(secondId, default);
        Assert.Equal(2, second.Account.CycleNumber);
        Assert.Null(second.Account.ClosedAt);
        Assert.Equal(30, second.Balance);
        Assert.Equal(30, second.Total);
        Assert.Single(second.Invoices);
        Assert.Empty(second.Receipts);
        Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"CustomerDebtAccounts\" SET \"ClosedAt\"={DateTimeOffset.UtcNow},\"ClosedBy\"={actor.UserId} WHERE \"Id\"={secondId}"))).SqlState);
        Assert.Equal(1, (await reads.ListAsync(new() { IsClosed = true }, default)).TotalCount);
        Assert.Equal(1, (await reads.ListAsync(new() { IsClosed = false }, default)).TotalCount);
        settled = await reads.StatementAsync(first.Id, default);
        Assert.Equal(20, settled.Total);
        Assert.Equal(20, settled.Paid);
        Assert.Equal(0, settled.Balance);
        Assert.Equal(invoice, settled.Invoices.Single().Id);
        Assert.Single(settled.Receipts);
        var newInvoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == nextInvoice);
        await sp.GetRequiredService<InvoicePaymentRecording>().RecordAsync(nextInvoice, new()
        {
            OperationId = Guid.NewGuid(), RowVersion = newInvoice.RowVersion,
            CustomerId = customer.Id, Amount = 30, PaymentMethod = PaymentMethod.Cash,
            CashAccount = AccountType.Treasury
        }, default);
        Assert.NotNull((await reads.AccountAsync(secondId, default)).ClosedAt);
        Assert.Equal(2, await db.CustomerDebtAccounts.CountAsync());
    }
}
