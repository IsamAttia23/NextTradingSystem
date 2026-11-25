using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Accounting;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using Npgsql;
using Xunit;
namespace NEXT.Tests;

public sealed class OpeningAndManualInvoiceTests
{
    [PostgresFact]
    public async Task ReviewedInventoryOpeningBalancesOnceIsImmutableAndReconciles()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        f.Db.Add(new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 3, AverageCost = 1.01m, TotalCost = 3.02m });
        await f.Db.SaveChangesAsync();
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection,
            configureServices: s => s.AddSingleton<ICurrentUserService>(actor));
        await using var scope = app.Services.CreateAsyncScope();
        var db = await AccountingFoundationTests.Activate(scope.ServiceProvider, actor);
        var service = scope.ServiceProvider.GetRequiredService<AccountingOpenings>();
        var checks = scope.ServiceProvider.GetRequiredService<AccountingReconciliation>();
        Assert.Equal(-3.02m, (await checks.ReadAsync(default)).Single(c => c.Account == AccountType.Inventory).Difference);
        var before = await service.PreviewAsync(AccountType.Inventory, default);
        var input = new ApproveOpeningInput { OperationId = Guid.NewGuid(), Account = AccountType.Inventory,
            Date = DateOnly.FromDateTime(DateTime.Now), VerifiedBalance = 3.02m, PositionHash = before.Hash, Reason = "Opening stock count document 1" };
        var id = await service.ApproveAsync(input, default);
        Assert.Equal(id, await service.ApproveAsync(input, default));
        Assert.Equal(1, await db.AccountOpenings.CountAsync());
        Assert.Equal(1, await db.LedgerEntries.CountAsync());
        Assert.All(await checks.ReadAsync(default), c => Assert.Equal(0, c.Difference));
        Assert.Equal(3.02m, await Net(db, AccountType.Inventory));
        Assert.Equal(-3.02m, await Net(db, AccountType.OpeningBalances));
        var after = await service.PreviewAsync(AccountType.Inventory, default);
        Assert.True(after.Approved);
        input.OperationId = Guid.NewGuid(); input.PositionHash = after.Hash;
        await Assert.ThrowsAsync<ConflictException>(() => service.ApproveAsync(input, default));
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"AccountOpenings\" SET \"Reason\" = 'Tampered'"));
        Assert.Equal("23514", error.SqlState);
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261123030514_AddInventoryFinancialPosting"));
        Assert.Equal(1, await db.AccountOpenings.CountAsync());
    }

    [PostgresFact]
    public async Task StaleTreasuryReviewCannotPostAndFreshApprovalUsesOnlyTheDifference()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection,
            configureServices: s => s.AddSingleton<ICurrentUserService>(actor));
        await using var scope = app.Services.CreateAsyncScope();
        var db = await AccountingFoundationTests.Activate(scope.ServiceProvider, actor);
        var service = scope.ServiceProvider.GetRequiredService<AccountingOpenings>();
        var position = await service.PreviewAsync(AccountType.Treasury, default);
        await scope.ServiceProvider.GetRequiredService<IAccountingPostingService>().PostAsync(AccountingFoundationTests.TreasuryOpening(10), default);
        var input = new ApproveOpeningInput { OperationId = Guid.NewGuid(), Account = AccountType.Treasury,
            Date = DateOnly.FromDateTime(DateTime.Now), VerifiedBalance = 100, PositionHash = position.Hash, Reason = "Verified cash count" };
        await Assert.ThrowsAsync<ConflictException>(() => service.ApproveAsync(input, default));
        Assert.Empty(await db.AccountOpenings.ToListAsync());
        Assert.Equal(10, await Net(db, AccountType.Treasury));
        input.PositionHash = (await service.PreviewAsync(AccountType.Treasury, default)).Hash;
        input.Date = new DateOnly(2026, 11, 20);
        await Assert.ThrowsAsync<AppValidationException>(() => service.ApproveAsync(input, default));
        input.Date = DateOnly.FromDateTime(DateTime.Now);
        await service.ApproveAsync(input, default);
        Assert.Equal(100, await Net(db, AccountType.Treasury));
        Assert.Equal(90, (await db.AccountOpenings.SingleAsync()).Difference);
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [PostgresFact]
    public async Task ManualOnlyInvoiceCanBeEditedConfirmedShippedAndSettledWithoutStockOrNewProducts()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        (await f.Db.Warehouses.SingleAsync(w => w.Id == f.Source)).IsDefault = true;
        await f.Db.SaveChangesAsync();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var token = WorkspaceUiTests.Token(await client.GetStringAsync("/Invoices/Create"));
        var id = Guid.NewGuid();
        using (var created = await WorkspaceUiTests.Post(client, "/Invoices/Create", token, ("OperationId", id.ToString()),
            ("CustomerName", "Manual invoice customer"), ("Lines[0].ManualName", "Special service"),
            ("Lines[0].Quantity", "2.5"), ("Lines[0].QuotedUnitPrice", "12.34")))
            Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var invoice = await Reload(f, id);
        Assert.Equal(30.85m, invoice.TotalAmount);
        var line = Assert.Single(invoice.Items);
        Assert.Null(line.ProductId); Assert.Equal("Special service", line.ManualName); Assert.Equal(0, line.UnitCost);
        var editForm = await client.GetStringAsync($"/Invoices/Edit/{id}");
        using (var edited = await WorkspaceUiTests.Post(client, $"/Invoices/Edit/{id}", WorkspaceUiTests.Token(editForm),
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("CustomerName", invoice.CustomerName!), ("Lines[0].ManualName", "Revised service"),
            ("Lines[0].Quantity", "3"), ("Lines[0].QuotedUnitPrice", "10")))
            Assert.True(edited.IsSuccessStatusCode, await edited.Content.ReadAsStringAsync());
        invoice = await Reload(f, id);
        Assert.Equal(30, invoice.TotalAmount);
        using (var confirmed = await WorkspaceUiTests.Post(client, $"/Invoices/Confirm/{id}", token,
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)), ("Amount", "10"), ("PaymentMethod", "Cash")))
            Assert.True(confirmed.IsSuccessStatusCode, await confirmed.Content.ReadAsStringAsync());
        invoice = await Reload(f, id);
        using (var shipped = await WorkspaceUiTests.Post(client, $"/Invoices/Fulfill/{id}", token,
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("TargetStatus", "Shipped"), ("CustomerPhone", "0912345678")))
            Assert.True(shipped.IsSuccessStatusCode, await shipped.Content.ReadAsStringAsync());
        invoice = await Reload(f, id);
        Assert.Equal(20, invoice.RemainingAmount);
        using (var settled = await WorkspaceUiTests.Post(client, $"/Invoices/RecordPayment/{id}", token,
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)), ("Amount", "20"), ("PaymentMethod", "Cash")))
            Assert.True(settled.IsSuccessStatusCode, await settled.Content.ReadAsStringAsync());
        invoice = await Reload(f, id);
        Assert.Equal(0, invoice.RemainingAmount);
        Assert.Equal(-30, await Net(f.Db, AccountType.Sales));
        Assert.Equal(30, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(0, await Net(f.Db, AccountType.CostOfSales));
        Assert.Empty(await f.Db.StockMovements.ToListAsync());
        Assert.Empty(await f.Db.Stocks.ToListAsync());
        Assert.Equal(1, await f.Db.Products.CountAsync());
        Assert.Contains("Revised service", await client.GetStringAsync($"/Invoices/Details/{id}"));
        var itemId = invoice.Items.Single().Id;
        var error = await Assert.ThrowsAsync<PostgresException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"InvoiceItems\" SET \"UnitPrice\" = 999 WHERE \"Id\" = {itemId}"));
        Assert.Equal("23514", error.SqlState);
    }

    [PostgresFact]
    public async Task ExistingProductPriceAndNameCannotBeDisguisedAsManualPricing()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true, withReceiptRate: true);
        (await f.Db.Warehouses.SingleAsync(w => w.Id == f.Source)).IsDefault = true;
        f.Db.Add(new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 10, TotalCost = 40, AverageCost = 4 });
        await f.Db.SaveChangesAsync();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app); await WorkspaceUiTests.Login(client, "ui-admin");
        var token = WorkspaceUiTests.Token(await client.GetStringAsync("/Invoices/Create"));
        using (var spoofed = await WorkspaceUiTests.Post(client, "/Invoices/Create", token, ("OperationId", Guid.NewGuid().ToString()),
            ("CustomerName", "Price guard"), ("Lines[0].ProductId", f.Product.ToString()), ("Lines[0].Quantity", "1"), ("Lines[0].QuotedUnitPrice", "1")))
            Assert.Equal(HttpStatusCode.Conflict, spoofed.StatusCode);
        using (var duplicate = await WorkspaceUiTests.Post(client, "/Invoices/Create", token, ("OperationId", Guid.NewGuid().ToString()),
            ("CustomerName", "Price guard"), ("Lines[0].ManualName", "  TEST PRODUCT  "), ("Lines[0].Quantity", "1"), ("Lines[0].QuotedUnitPrice", "1")))
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using (var imprecise = await WorkspaceUiTests.Post(client, "/Invoices/Create", token, ("OperationId", Guid.NewGuid().ToString()),
            ("CustomerName", "Price guard"), ("Lines[0].ManualName", "Service"), ("Lines[0].Quantity", "1"), ("Lines[0].QuotedUnitPrice", "1.234")))
            Assert.Equal(HttpStatusCode.BadRequest, imprecise.StatusCode);
        Assert.Empty(await f.Db.Invoices.ToListAsync());
        Assert.Equal(10, (await f.Db.Stocks.SingleAsync()).Quantity);
        var openingForm = await client.GetStringAsync("/FinancialAccounts/ApproveOpening?account=Inventory");
        var hash = System.Text.RegularExpressions.Regex.Match(openingForm, "name=\"PositionHash\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.Equal(64, hash.Length);
        using (var approved = await WorkspaceUiTests.Post(client, "/FinancialAccounts/ApproveOpening", WorkspaceUiTests.Token(openingForm),
            ("OperationId", Guid.NewGuid().ToString()), ("Account", "Inventory"), ("Date", DateTime.Now.ToString("yyyy-MM-dd")),
            ("VerifiedBalance", "40"), ("PositionHash", hash), ("Reason", "Reviewed initial stock")))
            Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync());
        var mixedId = Guid.NewGuid();
        using (var mixed = await WorkspaceUiTests.Post(client, "/Invoices/Create", token, ("OperationId", mixedId.ToString()),
            ("CustomerName", "Mixed invoice"), ("Lines[0].ProductId", f.Product.ToString()), ("Lines[0].Quantity", "2"), ("Lines[0].QuotedUnitPrice", "4"),
            ("Lines[1].ManualName", "Handling service"), ("Lines[1].Quantity", "1"), ("Lines[1].QuotedUnitPrice", "5")))
            Assert.True(mixed.IsSuccessStatusCode, await mixed.Content.ReadAsStringAsync());
        var invoice = await Reload(f, mixedId);
        Assert.Equal(13, invoice.TotalAmount);
        using (var confirmed = await WorkspaceUiTests.Post(client, $"/Invoices/Confirm/{mixedId}", token,
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)), ("Amount", "3"), ("PaymentMethod", "Cash")))
            Assert.True(confirmed.IsSuccessStatusCode, await confirmed.Content.ReadAsStringAsync());
        invoice = await Reload(f, mixedId);
        using (var shipped = await WorkspaceUiTests.Post(client, $"/Invoices/Fulfill/{mixedId}", token,
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)), ("TargetStatus", "Shipped"), ("CustomerPhone", "0912345678")))
            Assert.True(shipped.IsSuccessStatusCode, await shipped.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        Assert.Equal(32, (await f.Db.Stocks.SingleAsync()).TotalCost);
        Assert.Equal(32, await Net(f.Db, AccountType.Inventory));
        Assert.Equal(8, await Net(f.Db, AccountType.CostOfSales));
        Assert.Equal(-13, await Net(f.Db, AccountType.Sales));
        Assert.Equal(2, (await f.Db.StockMovements.SingleAsync()).Quantity);
        await using var verification = app.Services.CreateAsyncScope();
        Assert.All(await verification.ServiceProvider.GetRequiredService<AccountingReconciliation>().ReadAsync(default), row => Assert.Equal(0, row.Difference));
    }

    private static async Task<Invoice> Reload(PostgresInventoryTests.Database f, Guid id)
    { f.Db.ChangeTracker.Clear(); return await f.Db.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == id); }
    private static Task<decimal> Net(NEXTDbContext db, AccountType account) => db.AccountTransactions
        .Where(t => t.AccountId == Account.IdFor(account) && t.LedgerEntry.PostedAt != null).SumAsync(t => t.Debit - t.Credit);
}
