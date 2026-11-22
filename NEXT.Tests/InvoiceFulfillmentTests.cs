using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceFulfillmentTests
{
    [PostgresFact]
    public async Task ShipmentUsesValidBatchesOnceDeliveryDoesNotIssueAgainAndPaymentsContinue()
    {
        await using var f = await PostgresInventoryTests.Database.Create(expiry: true, migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = NewInvoice(actor.Id, f.Source, f.Product, "SHIP-BATCHES", 4);
        var stock = new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 10, ReservedQuantity = 4, AverageCost = 7.5m };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        f.Db.AddRange(invoice, stock, Batch(f, "EXPIRED", today.AddDays(-1), 3), Batch(f, "UNDATED", null, 1),
            Batch(f, "TODAY", today, 1), Batch(f, "NEAR", today.AddDays(1), 1), Batch(f, "LATER", today.AddDays(5), 4));
        await f.Db.SaveChangesAsync();
        var form = await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}?culture=en");
        Assert.Contains("data-modal-form", form);
        Assert.Contains("data-invoice-fulfillment", form);
        Assert.Contains("name=\"TargetStatus\"", form);
        Assert.DoesNotContain("name=\"WarehouseId\"", form);
        var token = WorkspaceUiTests.Token(form);
        var operation = Guid.NewGuid();
        var repeated = await Task.WhenAll(Fulfill(client, invoice, token, operation, InvoiceStatus.Shipped, "  Courier  "),
            Fulfill(client, invoice, token, operation, InvoiceStatus.Shipped, "Courier"));
        foreach (var response in repeated)
        {
            using (response) Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
        using (var changed = await Fulfill(client, invoice, token, operation, InvoiceStatus.Shipped, "Changed note"))
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using (var changedTarget = await Fulfill(client, invoice, token, operation, InvoiceStatus.Delivered, "Courier"))
            Assert.Equal(HttpStatusCode.Conflict, changedTarget.StatusCode);
        f.Db.ChangeTracker.Clear();
        var shipped = await f.Db.Invoices.AsNoTracking().SingleAsync();
        Assert.Equal(InvoiceStatus.Shipped, shipped.Status);
        Assert.Equal(10m, shipped.RemainingAmount);
        Assert.NotNull(shipped.LastActionAt);
        var firstHistory = await f.Db.InvoiceStatusHistory.SingleAsync();
        Assert.Equal(actor.FullName, firstHistory.ActorFullName);
        Assert.Equal("Courier", firstHistory.Reason);
        var afterStock = await f.Db.Stocks.AsNoTracking().SingleAsync();
        Assert.Equal(6m, afterStock.Quantity);
        Assert.Equal(0m, afterStock.ReservedQuantity);
        Assert.Equal(6m, afterStock.AvailableQuantity);
        var batches = await f.Db.Batches.AsNoTracking().ToDictionaryAsync(item => item.BatchNumber);
        Assert.Equal(3m, batches["EXPIRED"].Quantity);
        Assert.Equal(1m, batches["UNDATED"].Quantity);
        Assert.Equal(0m, batches["TODAY"].Quantity);
        Assert.Equal(0m, batches["NEAR"].Quantity);
        Assert.Equal(2m, batches["LATER"].Quantity);
        var movements = await f.Db.StockMovements.AsNoTracking().ToListAsync();
        Assert.Equal(3, movements.Count);
        Assert.Equal(4m, movements.Sum(item => item.Quantity));
        Assert.All(movements, item =>
        {
            Assert.Equal(StockMovementType.Sale, item.Type);
            Assert.Equal("Invoice", item.ReferenceType);
            Assert.Equal(invoice.Id, item.ReferenceId);
            Assert.Equal(7.5m, item.UnitCost);
            Assert.Contains("Batch:", item.Notes);
        });
        using (var stale = await Fulfill(client, invoice, token, Guid.NewGuid(), InvoiceStatus.Delivered))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var paid = await Pay(client, shipped, token, "2"))
            Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        shipped = await f.Db.Invoices.AsNoTracking().SingleAsync();
        var renamed = await f.Db.Users.SingleAsync(item => item.Id == actor.Id);
        renamed.FullName = "Delivery operator snapshot";
        await f.Db.SaveChangesAsync();
        var deliveryForm = await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}?targetStatus=Delivered&culture=ar");
        Assert.DoesNotContain("<select", deliveryForm);
        using (var delivered = await Fulfill(client, shipped, WorkspaceUiTests.Token(deliveryForm), Guid.NewGuid(), InvoiceStatus.Delivered, "Received"))
            Assert.True(delivered.IsSuccessStatusCode, await delivered.Content.ReadAsStringAsync());
        using (var lateRetry = await Fulfill(client, invoice, token, operation, InvoiceStatus.Shipped, "Courier"))
            Assert.True(lateRetry.IsSuccessStatusCode, await lateRetry.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        var final = await f.Db.Invoices.AsNoTracking().SingleAsync();
        Assert.Equal(InvoiceStatus.Delivered, final.Status);
        Assert.Equal(8m, final.RemainingAmount);
        using (var paid = await Pay(client, final, token, "8"))
            Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        final = await f.Db.Invoices.AsNoTracking().SingleAsync();
        Assert.Equal(InvoiceStatus.Delivered, final.Status);
        Assert.Equal(PaymentStatus.Paid, final.PaymentStatus);
        Assert.Equal(0m, final.RemainingAmount);
        Assert.Equal(3, await f.Db.StockMovements.CountAsync());
        Assert.Equal(6m, (await f.Db.Stocks.AsNoTracking().SingleAsync()).Quantity);
        var history = await f.Db.InvoiceStatusHistory.AsNoTracking().ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal("Delivery operator snapshot", history.Single(item => item.ToStatus == InvoiceStatus.Delivered).ActorFullName);
        Assert.Equal(actor.FullName, history.Single(item => item.ToStatus == InvoiceStatus.Shipped).ActorFullName);
        var details = WebUtility.HtmlDecode(await client.GetStringAsync($"/Invoices/Details/{invoice.Id}?culture=ar"));
        Assert.Contains("مسلّمة", details);
        Assert.DoesNotContain($"/Invoices/Fulfill/{invoice.Id}", details);
    }

    [PostgresFact]
    public async Task DirectDeliveryAndShipmentCompeteWithoutDoubleIssueAndRejectUnsupportedInvoices()
    {
        await using var f = await PostgresInventoryTests.Database.Create();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = NewInvoice(actor.Id, f.Source, f.Product, "FULFILL-CONCURRENT", 4);
        f.Db.AddRange(invoice, new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 10, ReservedQuantity = 4 });
        await f.Db.SaveChangesAsync();
        var token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}"));
        using (var badNote = await Fulfill(client, invoice, token, Guid.NewGuid(), InvoiceStatus.Shipped, new string('x', 501)))
            Assert.Equal(HttpStatusCode.BadRequest, badNote.StatusCode);
        using (var badTarget = await Fulfill(client, invoice, token, Guid.NewGuid(), InvoiceStatus.Pending))
            Assert.Equal(HttpStatusCode.BadRequest, badTarget.StatusCode);
        var paymentOperation = Guid.NewGuid();
        f.Db.Payments.Add(new Payment { Id = paymentOperation, PaymentNumber = "FULFILL-ID-COLLISION", Amount = 1,
            PaymentType = PaymentType.Receipt, PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        await f.Db.SaveChangesAsync();
        using (var reusedPayment = await Fulfill(client, invoice, token, paymentOperation, InvoiceStatus.Shipped))
            Assert.Equal(HttpStatusCode.Conflict, reusedPayment.StatusCode);
        var reserved = await f.Db.Stocks.SingleAsync();
        reserved.ReservedQuantity = 3;
        await f.Db.SaveChangesAsync();
        using (var insufficient = await Fulfill(client, invoice, token, Guid.NewGuid(), InvoiceStatus.Shipped))
            Assert.Equal(HttpStatusCode.BadRequest, insufficient.StatusCode);
        Assert.Empty(await f.Db.StockMovements.ToListAsync());
        reserved.ReservedQuantity = 4;
        await f.Db.SaveChangesAsync();
        var outcomes = await Task.WhenAll(Fulfill(client, invoice, token, Guid.NewGuid(), InvoiceStatus.Shipped),
            Fulfill(client, invoice, token, Guid.NewGuid(), InvoiceStatus.Delivered));
        Assert.Single(outcomes, response => response.IsSuccessStatusCode);
        Assert.Single(outcomes, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var outcome in outcomes) outcome.Dispose();
        f.Db.ChangeTracker.Clear();
        Assert.Equal(6m, (await f.Db.Stocks.AsNoTracking().SingleAsync()).Quantity);
        Assert.Equal(0m, (await f.Db.Stocks.AsNoTracking().SingleAsync()).ReservedQuantity);
        Assert.Single(await f.Db.StockMovements.ToListAsync());
        Assert.Single(await f.Db.InvoiceStatusHistory.ToListAsync());
        foreach (var kind in new[] { "Pending", "Cancelled", "Delivered", "Legacy", "Purchase", "Return" })
        {
            var other = NewInvoice(actor.Id, f.Source, f.Product, "UNAVAILABLE-" + kind, 2);
            if (kind is "Pending" or "Cancelled" or "Delivered") other.Status = Enum.Parse<InvoiceStatus>(kind);
            if (kind == "Legacy") other.LegacyInvoiceTypeCode = 0;
            if (kind == "Purchase") other.Type = InvoiceType.Purchase;
            if (kind == "Return") other.ParentInvoiceId = invoice.Id;
            f.Db.Add(other);
            await f.Db.SaveChangesAsync();
            using var rejected = await Fulfill(client, other, token, Guid.NewGuid(), InvoiceStatus.Shipped);
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
            using var hidden = await client.GetAsync($"/Invoices/Fulfill/{other.Id}?targetStatus=Shipped");
            Assert.Equal(HttpStatusCode.Conflict, hidden.StatusCode);
        }
        Assert.Single(await f.Db.StockMovements.ToListAsync());
    }

    [PostgresFact]
    public async Task ConcurrentInvoicesDoNotConsumeExpiredBatchesWhenValidStockIsExhausted()
    {
        await using var f = await PostgresInventoryTests.Database.Create(expiry: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var first = NewInvoice(actor.Id, f.Source, f.Product, "VALID-FIRST", 2);
        var second = NewInvoice(actor.Id, f.Source, f.Product, "VALID-SECOND", 2);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        f.Db.AddRange(first, second, new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 10, ReservedQuantity = 4 },
            Batch(f, "VALID", today.AddDays(1), 2), Batch(f, "OLD", today.AddDays(-1), 8));
        await f.Db.SaveChangesAsync();
        var token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Fulfill/{first.Id}"));
        var results = await Task.WhenAll(Fulfill(client, first, token, Guid.NewGuid(), InvoiceStatus.Shipped),
            Fulfill(client, second, token, Guid.NewGuid(), InvoiceStatus.Delivered));
        Assert.Single(results, response => response.IsSuccessStatusCode);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.BadRequest);
        foreach (var result in results) result.Dispose();
        f.Db.ChangeTracker.Clear();
        var stock = await f.Db.Stocks.AsNoTracking().SingleAsync();
        Assert.Equal(8m, stock.Quantity);
        Assert.Equal(2m, stock.ReservedQuantity);
        Assert.Equal(8m, (await f.Db.Batches.SingleAsync(item => item.BatchNumber == "OLD")).Quantity);
        Assert.Equal(0m, (await f.Db.Batches.SingleAsync(item => item.BatchNumber == "VALID")).Quantity);
        Assert.Single(await f.Db.InvoiceStatusHistory.ToListAsync());
        Assert.Single(await f.Db.Invoices.Where(item => item.Status == InvoiceStatus.Confirmed).ToListAsync());
        Assert.Equal(2m, (await f.Db.StockMovements.SingleAsync()).Quantity);
    }

    [PostgresFact]
    public async Task FailedLedgerWriteRollsBackStockBatchesHistoryAndVersionThenAllowsRetry()
    {
        await using var f = await PostgresInventoryTests.Database.Create(expiry: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = NewInvoice(actor.Id, f.Source, f.Product, "FULFILL-ROLLBACK", 4);
        f.Db.AddRange(invoice, new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 10, ReservedQuantity = 4 },
            Batch(f, "VALID", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), 10));
        await f.Db.SaveChangesAsync();
        await f.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_invoice_sale() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected sale ledger failure'; END $$;
            CREATE TRIGGER reject_invoice_sale BEFORE INSERT ON "StockMovements"
            FOR EACH ROW EXECUTE FUNCTION reject_invoice_sale();
            """);
        var token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}"));
        var operation = Guid.NewGuid();
        using (var failed = await Fulfill(client, invoice, token, operation, InvoiceStatus.Delivered))
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        f.Db.ChangeTracker.Clear();
        var unchanged = await f.Db.Invoices.AsNoTracking().SingleAsync();
        Assert.Equal(InvoiceStatus.Confirmed, unchanged.Status);
        Assert.Equal(invoice.RowVersion, unchanged.RowVersion);
        Assert.Equal(10m, (await f.Db.Stocks.AsNoTracking().SingleAsync()).Quantity);
        Assert.Equal(4m, (await f.Db.Stocks.AsNoTracking().SingleAsync()).ReservedQuantity);
        Assert.Equal(10m, (await f.Db.Batches.AsNoTracking().SingleAsync()).Quantity);
        Assert.Empty(await f.Db.InvoiceStatusHistory.ToListAsync());
        Assert.Empty(await f.Db.StockMovements.ToListAsync());
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_invoice_sale ON \"StockMovements\";");
        using var retry = await Fulfill(client, invoice, token, operation, InvoiceStatus.Delivered);
        Assert.True(retry.IsSuccessStatusCode, await retry.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        Assert.Equal(InvoiceStatus.Delivered, (await f.Db.Invoices.SingleAsync()).Status);
        Assert.Equal(6m, (await f.Db.Stocks.SingleAsync()).Quantity);
        Assert.Equal(0m, (await f.Db.Stocks.SingleAsync()).ReservedQuantity);
        Assert.Single(await f.Db.StockMovements.ToListAsync());
    }

    [PdfFact]
    public async Task FulfillmentModalUpdatesActionsPreservesDiscardAndSubmitsShipmentAndDeliveryOnMobile()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        Guid id;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var actor = await db.Users.SingleAsync(item => item.UserName == "ui-admin");
            var product = new Product { Name = "منتج الشحن / Dispatch product" };
            var warehouse = new Warehouse { Name = "Fulfillment warehouse", Code = "FULFILL" };
            db.AddRange(product, warehouse);
            await db.SaveChangesAsync();
            var invoice = NewInvoice(actor.Id, warehouse.Id, product.Id, "INV-11202026-UI", 4);
            db.AddRange(invoice, new Stock { ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 10, ReservedQuantity = 4 });
            await db.SaveChangesAsync();
            id = invoice.Id;
        }
        var browser = await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15000);
        using var client = WorkspaceUiTests.Client(app);
        var origin = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin + "/Account/Login?culture=ar");
        await page.Locator("[name=UserName]").FillAsync("ui-admin");
        await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(origin + "/");
        await page.GotoAsync(origin + $"/Invoices/Details/{id}?culture=ar");
        Assert.Equal(1, await page.Locator("[data-modal-url*='Fulfill']").CountAsync());
        await page.Locator("[data-modal-url*='Fulfill']").ClickAsync();
        await page.Locator("#workspaceModal.show [data-invoice-fulfillment]").WaitForAsync();
        await page.Locator("#modalBody [name=TargetStatus]").SelectOptionAsync("Delivered");
        Assert.Equal("تسليم مباشر", await page.Locator("#modalTitle").InnerTextAsync());
        await page.Locator("#modalBody [data-bs-dismiss=modal]").ClickAsync();
        await page.Locator("#modalDiscard").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await page.Locator("[data-keep-editing]").ClickAsync();
        Assert.Equal("تسليم مباشر", await page.Locator("#modalTitle").InnerTextAsync());
        await page.Locator("#modalBody [name=TargetStatus]").SelectOptionAsync("Shipped");
        await page.Locator("#modalBody [name=Note]").FillAsync("شُحنت للعميل");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        Assert.True(await page.Locator("#modalBody .table-responsive").EvaluateAsync<bool>("el => el.scrollWidth <= el.clientWidth"));
        var qa = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(qa))
        {
            Directory.CreateDirectory(qa);
            await page.ScreenshotAsync(new() { Path = Path.Combine(qa, "fulfillment-mobile.png"), FullPage = true });
        }
        await page.Locator("#modalBody [type=submit]").ClickAsync();
        await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Assert.Contains("مشحونة", await page.Locator("#main .page-heading p").InnerTextAsync());
        Assert.Equal(1, await page.Locator("[data-modal-url*='Fulfill']").CountAsync());
        await page.Locator("[data-modal-url*='Fulfill']").ClickAsync();
        await page.Locator("#workspaceModal.show [data-invoice-fulfillment]").WaitForAsync();
        Assert.Equal(0, await page.Locator("#modalBody select").CountAsync());
        Assert.Equal("تأكيد التسليم", await page.Locator("#modalTitle").InnerTextAsync());
        await page.Locator("#modalBody [type=submit]").ClickAsync();
        await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Assert.Equal(0, await page.Locator("[data-modal-url*='Fulfill']").CountAsync());
        Assert.Equal(1, await page.Locator("[data-modal-url*='RecordPayment']").CountAsync());
        await using var check = app.Services.CreateAsyncScope();
        var finalDb = check.ServiceProvider.GetRequiredService<NEXTDbContext>();
        Assert.Equal(InvoiceStatus.Delivered, (await finalDb.Invoices.SingleAsync()).Status);
        Assert.Equal(6m, (await finalDb.Stocks.SingleAsync()).Quantity);
        Assert.Equal(0m, (await finalDb.Stocks.SingleAsync()).ReservedQuantity);
        Assert.Single(await finalDb.StockMovements.ToListAsync());
        Assert.Equal(2, await finalDb.InvoiceStatusHistory.CountAsync());
    }

    private static Invoice NewInvoice(Guid actor, Guid warehouse, Guid product, string number, decimal quantity) => new()
    {
        InvoiceNumber = number, UserId = actor, WarehouseId = warehouse, CustomerName = "Fulfillment customer",
        Status = InvoiceStatus.Confirmed, SubTotal = 10, TotalAmount = 10, RemainingAmount = 10,
        Items = [new InvoiceItem { ProductId = product, Quantity = quantity / 2, UnitPrice = 10 / quantity, TotalAmount = 5 },
            new InvoiceItem { ProductId = product, Quantity = quantity / 2, UnitPrice = 10 / quantity, TotalAmount = 5 }]
    };

    private static ProductBatch Batch(PostgresInventoryTests.Database f, string number, DateOnly? expiry, decimal quantity) => new()
    {
        ProductId = f.Product, WarehouseId = f.Source, BatchNumber = number, ExpiryDate = expiry, Quantity = quantity, CostPrice = 7.5m
    };

    private static Task<HttpResponseMessage> Fulfill(HttpClient client, Invoice invoice, string token, Guid operation,
        InvoiceStatus target, string note = "") => WorkspaceUiTests.Post(client, $"/Invoices/Fulfill/{invoice.Id}", token,
            ("OperationId", operation.ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("TargetStatus", target.ToString()), ("Note", note));

    private static Task<HttpResponseMessage> Pay(HttpClient client, Invoice invoice, string token, string amount) =>
        WorkspaceUiTests.Post(client, $"/Invoices/RecordPayment/{invoice.Id}", token,
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("Amount", amount), ("PaymentMethod", "Cash"));
}
