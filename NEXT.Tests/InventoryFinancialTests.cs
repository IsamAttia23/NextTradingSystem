using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;
using NEXT.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace NEXT.Tests;

public sealed class InventoryFinancialTests
{
    [Fact]
    public void WeightedValueAndLastUnitPreserveEveryPenny()
    {
        var value = InventoryValuation.ReceiptValue(1, 1) + InventoryValuation.ReceiptValue(2, 1.01m);
        Assert.Equal(3.02m, value);
        Assert.Equal(1.01m, InventoryValuation.Average(3, value));
        var first = InventoryValuation.IssueValue(3, value, 1);
        var second = InventoryValuation.IssueValue(2, value - first, 1);
        var last = InventoryValuation.IssueValue(1, value - first - second, 1);
        Assert.Equal(3.02m, first + second + last);
        Assert.Equal(0, value - first - second - last);
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryValuation.IssueValue(1, 1, 2));
    }

    [PostgresFact]
    public async Task ReceiptAndTransfersReconcileCarryingValueWithoutInventingSupplierInvoices()
    {
        await using var f = await StockWithRoundingDifference();
        var stock = await f.Db.Stocks.SingleAsync();
        Assert.Equal(3.02m, stock.TotalCost);
        Assert.Equal(1.01m, stock.AverageCost);
        Assert.Equal(3.02m, await Net(f.Db, AccountType.Inventory));
        Assert.Equal(-3.02m, await Net(f.Db, AccountType.GoodsReceivedNotInvoiced));
        Assert.Equal(0, await Net(f.Db, AccountType.Payables));
        Assert.Equal(2, await f.Db.LedgerEntries.CountAsync());
        Assert.All(await f.Db.StockMovements.ToListAsync(), m => Assert.Equal(f.Supplier, m.SupplierId));
        await f.Service().TransferAsync(f.Transfer(1));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(3.02m, await f.Db.Stocks.SumAsync(s => s.TotalCost));
        Assert.Equal(2, await f.Db.LedgerEntries.CountAsync());
        var remaining = await f.Db.Stocks.SingleAsync(s => s.WarehouseId == f.Source);
        await f.Service().TransferAsync(f.Transfer(remaining.Quantity));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(0, (await f.Db.Stocks.SingleAsync(s => s.WarehouseId == f.Source)).TotalCost);
        Assert.Equal(3.02m, (await f.Db.Stocks.SingleAsync(s => s.WarehouseId == f.Target)).TotalCost);
        var transfers = await f.Db.StockMovements.Where(m => m.ReferenceType == "Transfer").ToListAsync();
        foreach (var operation in transfers.GroupBy(m => m.ReferenceId))
            Assert.Equal(operation.Single(m => m.Type == StockMovementType.TransferOut).TotalCost,
                operation.Single(m => m.Type == StockMovementType.TransferIn).TotalCost);
        var changed = await f.Db.StockMovements.FirstAsync();
        changed.TotalCost += 1;
        await Assert.ThrowsAsync<ConflictException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();
        var id = changed.Id;
        var error = await Assert.ThrowsAsync<PostgresException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"StockMovements\" SET \"TotalCost\" = \"TotalCost\" + 1 WHERE \"Id\" = {id}"));
        Assert.Equal("23514", error.SqlState);
        var receiptId = (await f.Db.StockMovements.FirstAsync(m => m.ReferenceType == "Receipt")).ReferenceId;
        var extra = Guid.NewGuid();
        var extension = await Assert.ThrowsAsync<PostgresException>(() => f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "StockMovements" ("Id", "ProductId", "WarehouseId", "Type", "Quantity", "UnitCost", "TotalCost", "ReferenceType", "ReferenceId", "CreatedAt")
            VALUES ({extra}, {f.Product}, {f.Source}, 7, 1, 0, 0, 'Receipt', {receiptId}, now())
            """));
        Assert.Equal("23514", extension.SqlState);
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DispatchPostsExactCostAndFreeGoodsStillConsumeInventory(bool free)
    {
        await using var f = await StockWithRoundingDifference();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(u => u.UserName == "ui-admin");
        var invoice = await Invoice(f, actor.Id, free);
        using var response = await Dispatch(client, invoice);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        Assert.Equal(0, (await f.Db.Stocks.SingleAsync()).TotalCost);
        Assert.Equal(0, (await f.Db.Stocks.SingleAsync()).Quantity);
        Assert.Equal(0, await Net(f.Db, AccountType.Inventory));
        Assert.Equal(3.02m, await Net(f.Db, AccountType.CostOfSales));
        Assert.Equal(free ? 0 : -10, await Net(f.Db, AccountType.Sales));
        var sale = await f.Db.LedgerEntries.Include(e => e.Lines).SingleAsync(e => e.InvoiceId == invoice.Id);
        Assert.Equal(LedgerEntryKind.SalesRecognition, sale.Kind);
        Assert.Equal(free ? 2 : 4, sale.Lines.Count);
        Assert.Equal(3.02m, await f.Db.StockMovements.Where(m => m.ReferenceType == "Invoice").SumAsync(m => m.TotalCost));
        await using var scope = app.Services.CreateAsyncScope();
        var actorService = InventoryTestServices.Posting(scope.ServiceProvider.GetRequiredService<NEXTDbContext>(), f.Branch);
        var stockReceipt = await f.Db.LedgerEntries.FirstAsync(e => e.Kind == LedgerEntryKind.GoodsReceipt);
        // A separate general reversal would detach stock from its source receipt.
        var reversal = new PostingRequestDto(Guid.NewGuid(), LedgerEntryKind.Reversal, AccountingReferenceType.Adjustment,
            stockReceipt.Id, DateOnly.FromDateTime(DateTime.Now), "Invalid inventory reversal",
            [new(AccountType.GoodsReceivedNotInvoiced, 1, 0), new(AccountType.Inventory, 0, 1)], ReversesEntryId: stockReceipt.Id);
        await Assert.ThrowsAsync<ConflictException>(() => actorService.PostAsync(reversal, default));
    }

    [PostgresFact]
    public async Task CostPostingFailureRollsBackDispatchStockMovementsAndReceivable()
    {
        await using var f = await StockWithRoundingDifference();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(u => u.UserName == "ui-admin");
        var invoice = await Invoice(f, actor.Id, false);
        await f.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_cost_posting() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."AccountId" = 'a1100000-0000-0000-0000-000000000010' THEN
                    RAISE EXCEPTION 'cost posting failed' USING ERRCODE = '23514'; END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_cost BEFORE INSERT ON "AccountTransactions" FOR EACH ROW EXECUTE FUNCTION reject_cost_posting();
            """);
        using var response = await Dispatch(client, invoice);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(InvoiceStatus.Confirmed, (await f.Db.Invoices.SingleAsync()).Status);
        Assert.Equal(3.02m, (await f.Db.Stocks.SingleAsync()).TotalCost);
        Assert.Equal(3, (await f.Db.Stocks.SingleAsync()).ReservedQuantity);
        Assert.Equal(2, await f.Db.StockMovements.CountAsync());
        Assert.Equal(2, await f.Db.LedgerEntries.CountAsync());
        Assert.Empty(await f.Db.DebtItems.ToListAsync());
    }

    [PostgresFact]
    public async Task UpgradePreservesRecordedLegacyValueAndDoesNotCreateHistoricalJournals()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var migrator = f.Db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261122181207_EnforcePostedSourceIntegrity");
        var id = Guid.NewGuid();
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Stocks" ("Id", "ProductId", "WarehouseId", "Quantity", "ReservedQuantity", "AverageCost", "LastUpdated", "CreatedAt", "RowVersion")
            VALUES ({id}, {f.Product}, {f.Source}, 3, 0, 1.01, now(), now(), {Guid.NewGuid().ToByteArray()})
            """);
        await migrator.MigrateAsync();
        f.Db.ChangeTracker.Clear();
        Assert.Equal(3.03m, (await f.Db.Stocks.SingleAsync()).TotalCost); // old precise value cannot be recovered
        Assert.Empty(await f.Db.LedgerEntries.ToListAsync());
        Assert.Equal(15, await f.Db.Accounts.CountAsync());
        await migrator.MigrateAsync("20261122181207_EnforcePostedSourceIntegrity");
        await migrator.MigrateAsync();
        Assert.Equal(3.03m, (await f.Db.Stocks.AsNoTracking().SingleAsync()).TotalCost);
    }

    private static async Task<PostgresInventoryTests.Database> StockWithRoundingDifference()
    {
        var f = await PostgresInventoryTests.Database.Create(migrate: true, withReceiptRate: true);
        try
        {
            await f.SetProductCostAsync(1);
            await f.Service().ReceiveAsync(f.Receipt(1));
            await f.SetProductCostAsync(1.01m);
            await f.Service().ReceiveAsync(f.Receipt(2));
            f.Db.ChangeTracker.Clear();
            return f;
        }
        catch { await f.DisposeAsync(); throw; }
    }

    private static async Task<Invoice> Invoice(PostgresInventoryTests.Database f, Guid actor, bool free)
    {
        var invoice = new Invoice { InvoiceNumber = "VALUE-" + Guid.NewGuid().ToString("N"), UserId = actor,
            WarehouseId = f.Source, CustomerName = "Inventory value customer", Status = InvoiceStatus.Confirmed,
            SubTotal = free ? 0 : 10, TotalAmount = free ? 0 : 10, RemainingAmount = free ? 0 : 10,
            Items = [new InvoiceItem { ProductId = f.Product, Quantity = 3, UnitPrice = free ? 0 : 10m / 3, TotalAmount = free ? 0 : 10 }] };
        var stock = await f.Db.Stocks.SingleAsync();
        stock.ReservedQuantity = 3;
        f.Db.Add(invoice);
        await f.Db.SaveChangesAsync();
        return invoice;
    }
    private static async Task<HttpResponseMessage> Dispatch(HttpClient client, Invoice invoice)
    {
        var form = await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}?culture=en");
        return await WorkspaceUiTests.Post(client, $"/Invoices/Fulfill/{invoice.Id}", WorkspaceUiTests.Token(form),
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("TargetStatus", "Shipped"), ("CustomerPhone", "0912345678"));
    }
    private static Task<decimal> Net(NEXTDbContext db, AccountType account) => db.AccountTransactions
        .Where(t => t.AccountId == Account.IdFor(account) && t.LedgerEntry.PostedAt != null).SumAsync(t => t.Debit - t.Credit);
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXT_RUN_POSTGRES_TESTS") != "1")
            Skip = "Set NEXT_RUN_POSTGRES_TESTS=1 for local PostgreSQL integration checks.";
    }
}
