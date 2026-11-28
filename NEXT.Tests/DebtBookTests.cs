using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.DTOs.Invoices;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Accounting;
using NEXT.Application.Features.Invoices;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using Npgsql;
using Xunit;
namespace NEXT.Tests;

public sealed class DebtBookTests
{
    [PostgresFact]
    public async Task ShipmentOpensOneCustomerAccountWithdrawalsAndCrossInvoicePaymentsRemainAtomicAndPrintable()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection, configureServices: s => { s.AddSingleton<ICurrentUserService>(actor); s.AddLogging(l => l.AddConsole().SetMinimumLevel(LogLevel.Error)); });
        app.Configuration["Inventory:AllowInvoiceOverselling"] = "true";
        await using var scope = app.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = await AccountingFoundationTests.Activate(sp, actor);
        var customer = await Prepare(db, f.Source, f.Product);
        var first = await Sell(sp, db, customer, f.Product, 2, InvoiceStatus.Shipped);
        var account = await db.CustomerDebtAccounts.AsNoTracking().SingleAsync();
        Assert.Equal(customer.Id, account.CustomerId);
        var reads = sp.GetRequiredService<DebtBookReads>();
        var statement = await reads.StatementAsync(account.Id, default);
        Assert.Equal(20, statement.Balance);
        var withdrawal = new DebtBookWithdrawalInput { OperationId = Guid.NewGuid(), CustomerId = customer.Id, CustomerName = customer.Name, CustomerPhone = customer.Phone, RowVersion = statement.Account.RowVersion, Lines = [new() { ProductId = f.Product, Quantity = 3, QuotedUnitPrice = 10 }] };
        var withdraws = sp.GetRequiredService<DebtBookWithdrawals>();
        var second = await withdraws.WithdrawAsync(account.Id, withdrawal, default);
        Assert.Equal(second, await withdraws.WithdrawAsync(account.Id, withdrawal, default));
        statement = await reads.StatementAsync(account.Id, default);
        Assert.Equal(2, statement.Invoices.Count);
        Assert.Equal(50, statement.Total);
        Assert.Equal(50, statement.Balance);
        Assert.Equal(-5, (await db.Stocks.AsNoTracking().SingleAsync()).Quantity);
        var receive = sp.GetRequiredService<DebtBookPayments>();
        await Assert.ThrowsAsync<AppValidationException>(() => receive.ReceiveAsync(account.Id, new()
        {
            OperationId = Guid.NewGuid(), RowVersion = statement.Account.RowVersion, Amount = 1,
            PaymentMethod = PaymentMethod.Cash, CashAccount = AccountType.Treasury, Notes = "   "
        }, default));
        Assert.Empty(await db.DebtAccountPayments.ToListAsync());
        var input = new DebtBookPaymentInput { OperationId = Guid.NewGuid(), RowVersion = statement.Account.RowVersion, Amount = 35, PaymentMethod = PaymentMethod.Wallet, CashAccount = AccountType.Wallet, Notes = "Account payment" };
        var paid = await receive.ReceiveAsync(account.Id, input, default);
        Assert.Equal(paid, await receive.ReceiveAsync(account.Id, input, default));
        Assert.Equal(2, await db.Payments.CountAsync(p => p.DebtAccountPaymentId == paid));
        var invoices = await db.Invoices.AsNoTracking().OrderBy(i => i.InvoiceNumber).ToListAsync();
        Assert.Equal(0, invoices.Single(i => i.Id == first).RemainingAmount);
        Assert.Equal(15, invoices.Single(i => i.Id == second).RemainingAmount);
        statement = await reads.StatementAsync(account.Id, default);
        Assert.Equal(50, statement.Total);
        Assert.Equal(35, statement.Paid);
        Assert.Equal(15, statement.Balance);
        Assert.Single(statement.Receipts);
        Assert.Equal("Account payment", statement.Receipts.Single().Description);
        input.Amount = 1;
        await Assert.ThrowsAsync<ConflictException>(() => receive.ReceiveAsync(account.Id, input, default));
        input.OperationId = Guid.NewGuid();
        await Assert.ThrowsAsync<ConflictException>(() => receive.ReceiveAsync(account.Id, input, default));
        input.RowVersion = statement.Account.RowVersion;
        input.Amount = 16;
        await Assert.ThrowsAsync<AppValidationException>(() => receive.ReceiveAsync(account.Id, input, default));
        var controls = sp.GetRequiredService<FinancialControls>();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var period = await controls.CreatePeriodAsync(new() { Name = "Close for test", Start = today, End = today, Reason = "Reviewed" }, default);
        await controls.ChangePeriodAsync(new() { Id = period, Version = 1, Close = true, Reason = "Reviewed" }, default);
        input.Amount = 1;
        await Assert.ThrowsAsync<ConflictException>(() => receive.ReceiveAsync(account.Id, input, default));
        Assert.Equal(1, await db.DebtAccountPayments.CountAsync());
        Assert.Equal(15, (await reads.StatementAsync(account.Id, default)).Balance);
        await controls.ChangePeriodAsync(new() { Id = period, Version = 2, Close = false, Reason = "Approved" }, default);
        input.OperationId = Guid.NewGuid();
        input.RowVersion = (await reads.AccountAsync(account.Id, default)).RowVersion;
        input.Amount = 15;
        await receive.ReceiveAsync(account.Id, input, default);
        statement = await reads.StatementAsync(account.Id, default);
        Assert.Equal(0, statement.Balance);
        Assert.Equal(50, statement.Paid);
        foreach (var sql in new[] { "UPDATE \"DebtAccountPayments\" SET \"Amount\"=1", "DELETE FROM \"DebtAccountPayments\"" })
            Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => f.Db.Database.ExecuteSqlRawAsync(sql))).SqlState);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/DebtBookDocuments/Print/" + account.Id + "?culture=ar"));
        Assert.Contains(customer.Name, html);
        Assert.Contains("البضاعة", html);
        Assert.Contains("ACC-", html);
        Assert.Contains("50", html);
    }
    [PostgresFact]
    public async Task OneAccountPaymentCanSettleAnInvoiceAndAnOpeningBalanceTogether()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection, configureServices: s => { s.AddSingleton<ICurrentUserService>(actor); s.AddLogging(l => l.AddConsole().SetMinimumLevel(LogLevel.Error)); });
        app.Configuration["Inventory:AllowInvoiceOverselling"] = "true";
        await using var scope = app.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = await AccountingFoundationTests.Activate(sp, actor);
        var customer = await Prepare(db, f.Source, f.Product);
        await Sell(sp, db, customer, f.Product, 2, InvoiceStatus.Delivered);
        var account = await db.CustomerDebtAccounts.AsNoTracking().SingleAsync();
        var debt = DebtItem.Create(account.Id, DebtItemKind.OpeningBalance, null, 30, DateOnly.FromDateTime(DateTime.Now), "Reviewed opening", actor.UserId!.Value);
        var work = sp.GetRequiredService<IUnitOfWork>();
        await work.ExecuteReadCommittedAsync(async ct => { db.DebtItems.Add(debt); await work.SaveChangesAsync(ct); await sp.GetRequiredService<IAccountingPostingService>().PostAsync(new(Guid.NewGuid(), LedgerEntryKind.OpeningBalance, AccountingReferenceType.DebtOpening, debt.Id, debt.EffectiveDate, "Opening", [new(AccountType.Receivables, 30, 0, customer.Id, debt.Id), new(AccountType.OpeningBalances, 0, 30, customer.Id)]), ct); return true; }, default);
        var reads = sp.GetRequiredService<DebtBookReads>();
        var statement = await reads.StatementAsync(account.Id, default);
        Assert.Equal(50, statement.Balance);
        await sp.GetRequiredService<DebtBookPayments>().ReceiveAsync(account.Id, new() { OperationId = Guid.NewGuid(), RowVersion = statement.Account.RowVersion, Notes = "Settlement for withdrawn goods", Amount = 30, PaymentMethod = PaymentMethod.Cash, CashAccount = AccountType.Treasury }, default);
        statement = await reads.StatementAsync(account.Id, default);
        Assert.Equal(20, statement.Balance);
        Assert.Equal(30, statement.Paid);
        Assert.Equal(50, statement.Total);
        Assert.Equal(10, (await db.Payments.SingleAsync(p => p.Purpose == PaymentPurpose.DebtReceipt)).Amount);
    }
    [PostgresFact]
    public async Task ConcurrentAccountPaymentsRejectStaleRequestsAndIdenticalRetriesPostOnce()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection, configureServices: s => s.AddSingleton<ICurrentUserService>(actor));
        app.Configuration["Inventory:AllowInvoiceOverselling"] = "true";
        Guid accountId;
        byte[] version;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var db = await AccountingFoundationTests.Activate(sp, actor);
            var c = await Prepare(db, f.Source, f.Product);
            await Sell(sp, db, c, f.Product, 5, InvoiceStatus.Delivered);
            var a = await db.CustomerDebtAccounts.SingleAsync();
            accountId = a.Id;
            version = a.RowVersion;
        }
        async Task<bool> Pay(Guid operation, decimal amount, byte[] rowVersion)
        {
            await using var scope = app.Services.CreateAsyncScope();
            try
            { await scope.ServiceProvider.GetRequiredService<DebtBookPayments>().ReceiveAsync(accountId, new() { OperationId = operation, Notes = "Settlement for withdrawn goods", Amount = amount, RowVersion = rowVersion, PaymentMethod = PaymentMethod.Cash, CashAccount = AccountType.Treasury }, default); return true; }
            catch (ConflictException) { return false; }
        }
        var competing = await Task.WhenAll(Pay(Guid.NewGuid(), 35, version), Pay(Guid.NewGuid(), 35, version));
        Assert.Single(competing, x => x);
        await using (var scope = app.Services.CreateAsyncScope())
            version = (await scope.ServiceProvider.GetRequiredService<DebtBookReads>().AccountAsync(accountId, default)).RowVersion;
        var id = Guid.NewGuid();
        Assert.All(await Task.WhenAll(Pay(id, 15, version), Pay(id, 15, version)), value => Assert.True(value));
        Assert.Equal(2, await f.Db.DebtAccountPayments.CountAsync());
        Assert.Equal(50, await f.Db.Payments.SumAsync(p => p.Amount));
        Assert.Equal(0, (await f.Db.Invoices.SingleAsync()).RemainingAmount);
    }
    internal static async Task<Customer> Prepare(NEXTDbContext db, Guid warehouseId, Guid productId)
    {
        var warehouse = await db.Warehouses.AsTracking().SingleAsync(w => w.Id == warehouseId);
        warehouse.IsDefault = true;
        var product = await db.Products.AsTracking().SingleAsync(p => p.Id == productId);
        product.CostPrice = 10;
        product.RetailPrice = 0;
        var customer = new Customer { Name = "محمد / Mohammed", Phone = "+249123456789" };
        db.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }
    internal static async Task<Guid> Sell(IServiceProvider sp, NEXTDbContext db, Customer c, Guid product, decimal quantity, InvoiceStatus target)
    {
        var s = await sp.GetRequiredService<InvoiceCreation>().CreateAsync(new() { OperationId = Guid.NewGuid(), CustomerId = c.Id, CustomerName = c.Name, Lines = [new() { ProductId = product, Quantity = quantity, QuotedUnitPrice = 10 }] }, default);
        var invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == s.Id);
        await sp.GetRequiredService<InvoiceConfirmation>().ConfirmAsync(s.Id, new() { OperationId = Guid.NewGuid(), RowVersion = invoice.RowVersion }, default);
        invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == s.Id);
        await sp.GetRequiredService<InvoiceFulfillment>().ExecuteAsync(s.Id, new() { OperationId = Guid.NewGuid(), RowVersion = invoice.RowVersion, TargetStatus = target }, default);
        return s.Id;
    }
}
