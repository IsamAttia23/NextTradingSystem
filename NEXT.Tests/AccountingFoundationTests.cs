using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class AccountingFoundationTests
{
    [Fact]
    public void FixedCatalogHasFifteenClassifiedStableAccountsAndTwoTechnicalAccounts()
    {
        var first = Account.SeedData();
        Assert.Equal(15, first.Count);
        Assert.Equal(15, first.Select(account => account.Id).Distinct().Count());
        Assert.Equal(15, first.Select(account => account.Code).Distinct().Count());
        Assert.Equal(2, first.Count(account => account.IsTechnical));
        Assert.Equal(first.Select(account => account.Id), Account.SeedData().Select(account => account.Id));
        Assert.Equal(DebitCreditSide.Credit, Account.Create(AccountType.Sales).NormalSide);
        Assert.Equal(DebitCreditSide.Debit, Account.Create(AccountType.Treasury).NormalSide);
        Assert.Throws<ArgumentOutOfRangeException>(() => Account.Create((AccountType)99));
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(100, 101, false)]
    [InlineData(100, 0, false)]
    [InlineData(100, -1, false)]
    [InlineData(0, 1, false)]
    [InlineData(100, 0.001, false)]
    public void SettlementRejectsExcessZeroNegativeAndFractionalAmounts(decimal remaining, decimal amount, bool allowed)
    {
        Assert.Equal(allowed, MoneyRules.CanSettle(remaining, amount));
        Assert.Equal(1.01m, MoneyRules.Round(1.005m));
        Assert.Equal(-1.01m, MoneyRules.Round(-1.005m));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(-1, 0)]
    [InlineData(0.001, 0)]
    public void ALineMustHaveExactlyOnePositiveTwoDecimalSide(decimal debit, decimal credit) =>
        Assert.Throws<ArgumentException>(() => AccountTransaction.Create(Guid.NewGuid(), 1, AccountType.Treasury, debit, credit));

    [Fact]
    public void EntryRequiresBalanceAndDebtLinesRequireAnOwnedDebtItem()
    {
        var id = Guid.NewGuid();
        var lines = new[] { AccountTransaction.Create(id, 1, AccountType.Treasury, 10, 0),
            AccountTransaction.Create(id, 2, AccountType.OpeningBalances, 0, 9) };
        Assert.Throws<ArgumentException>(() => LedgerEntry.Create(id, Guid.NewGuid(), LedgerEntryKind.OpeningBalance,
            AccountingReferenceType.Adjustment, Guid.NewGuid(), null, null, new(2026, 11, 21), "Opening", Guid.NewGuid(),
            "Operator", new string('A', 64), lines));
        Assert.Throws<ArgumentException>(() => AccountTransaction.Create(id, 1, AccountType.Receivables, 10, 0));
        Assert.Throws<ArgumentException>(() => AccountTransaction.Create(id, 1, AccountType.Treasury, 10, 0,
            Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task PostingIsIdempotentAndRejectsChangedRequestsAndDuplicateSources()
    {
        var actor = new TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        await using var scope = app.Services.CreateAsyncScope();
        var db = await Activate(scope.ServiceProvider, actor);
        var service = scope.ServiceProvider.GetRequiredService<IAccountingPostingService>();
        var input = TreasuryOpening(100);
        var id = await service.PostAsync(input, default);
        Assert.Equal(id, await service.PostAsync(input, default));
        await Assert.ThrowsAsync<ConflictException>(() => service.PostAsync(input with { Description = "Changed" }, default));
        await Assert.ThrowsAsync<ConflictException>(() => service.PostAsync(input with { OperationId = Guid.NewGuid() }, default));
        var row = await db.LedgerEntries.AsNoTracking().Include(entry => entry.Lines).SingleAsync();
        Assert.Equal(id, row.Id);
        Assert.NotNull(row.PostedAt);
        Assert.Equal(2, row.Lines.Count);
        Assert.Equal(100m, row.Lines.Sum(line => line.Debit));
        Assert.Equal(100m, row.Lines.Sum(line => line.Credit));
        var user = await db.Users.SingleAsync(item => item.Id == actor.UserId);
        var originalName = row.ActorName;
        user.FullName = "Renamed operator";
        await db.SaveChangesAsync();
        Assert.Equal(id, await service.PostAsync(input, default));
        Assert.Equal(originalName, (await db.LedgerEntries.AsNoTracking().SingleAsync()).ActorName);
    }

    [Fact]
    public async Task ReversalPreservesTheOriginalAndPeriodBalancesIncludeOpeningMovements()
    {
        var actor = new TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        await using var scope = app.Services.CreateAsyncScope();
        var db = await Activate(scope.ServiceProvider, actor);
        var posting = scope.ServiceProvider.GetRequiredService<IAccountingPostingService>();
        var reads = scope.ServiceProvider.GetRequiredService<IAccountingReadService>();
        var input = TreasuryOpening(100) with { Date = new(2026, 11, 20) };
        var id = await posting.PostAsync(input, default);
        var request = new ReverseEntryInput(Guid.NewGuid(), id, new(2026, 11, 21), "Opening entered incorrectly");
        var reversals = scope.ServiceProvider.GetRequiredService<IAccountingReversalService>();
        var reversed = await reversals.ReverseAsync(request, default);
        Assert.Equal(reversed, await reversals.ReverseAsync(request, default));
        await Assert.ThrowsAsync<ConflictException>(() => reversals.ReverseAsync(request with { OperationId = Guid.NewGuid() }, default));
        var accounts = await reads.GetAccountsAsync(new(2026, 11, 21), new(2026, 11, 21), default);
        Assert.Equal(15, accounts.Count);
        var cash = accounts.Single(item => item.Type == AccountType.Treasury);
        Assert.Equal(100m, cash.OpeningBalance);
        Assert.Equal(0m, cash.Debit);
        Assert.Equal(100m, cash.Credit);
        Assert.Equal(0m, cash.Balance);
        Assert.All(accounts, item => Assert.Equal(0m, item.Balance));
        var lines = await reads.GetTransactionsAsync(new AccountingQuery { Account = AccountType.Treasury }, default);
        Assert.Equal(2, lines.TotalCount);
        Assert.Equal(2, await db.LedgerEntries.CountAsync());
        Assert.Equal(4, await db.AccountTransactions.CountAsync());
        Assert.Equal("Treasury opening", (await db.LedgerEntries.AsNoTracking().SingleAsync(item => item.Id == id)).Description);
    }

    [Fact]
    public async Task ApplicationRejectsEditingDeletingAndAppendingToPostedEntries()
    {
        var actor = new TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        await using var scope = app.Services.CreateAsyncScope();
        var db = await Activate(scope.ServiceProvider, actor);
        var id = await scope.ServiceProvider.GetRequiredService<IAccountingPostingService>().PostAsync(TreasuryOpening(100), default);
        db.ChangeTracker.Clear();
        var entry = await db.LedgerEntries.Include(item => item.Lines).SingleAsync();
        db.Entry(entry).Property(item => item.Description).CurrentValue = "Changed";
        await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var line = await db.AccountTransactions.FirstAsync();
        db.Entry(line).Property(item => item.Debit).CurrentValue = 12;
        await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Remove(await db.Accounts.FirstAsync());
        Assert.Throws<ConflictException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        await db.LedgerEntries.SingleAsync();
        db.AccountTransactions.Add(AccountTransaction.Create(id, 3, AccountType.Treasury, 1, 0));
        await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task InvalidPostingAndCancellationLeaveNoFinancialRows()
    {
        var actor = new TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        await using var scope = app.Services.CreateAsyncScope();
        var db = await Activate(scope.ServiceProvider, actor);
        var posting = scope.ServiceProvider.GetRequiredService<IAccountingPostingService>();
        var invalid = TreasuryOpening(100) with { Lines = [new(AccountType.Treasury, 100, 0), new(AccountType.OpeningBalances, 0, 99)] };
        await Assert.ThrowsAsync<AppValidationException>(() => posting.PostAsync(invalid, default));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => posting.PostAsync(TreasuryOpening(100), cancelled.Token));
        Assert.Empty(await db.LedgerEntries.ToListAsync());
        Assert.Empty(await db.AccountTransactions.ToListAsync());
    }

    internal static PostingRequestDto TreasuryOpening(decimal amount) => new(Guid.NewGuid(), LedgerEntryKind.OpeningBalance,
        AccountingReferenceType.Adjustment, Guid.NewGuid(), new(2026, 11, 21), "Treasury opening",
        [new(AccountType.Treasury, amount, 0), new(AccountType.OpeningBalances, 0, amount)]);

    internal static async Task<NEXTDbContext> Activate(IServiceProvider services, TestActor actor)
    {
        var db = services.GetRequiredService<NEXTDbContext>();
        actor.UserId = (await db.Users.SingleAsync(item => item.UserName == "ui-admin")).Id;
        return db;
    }

    internal sealed class TestActor : ICurrentUserService { public Guid? UserId { get; set; } }
}
