using System.Net;
using Microsoft.EntityFrameworkCore;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoicePaymentsTests
{
    [PostgresFact]
    public async Task LaterPaymentsIncludeConfirmationAndAreIdempotentThroughFullSettlement()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var name = actor.FullName;
        var invoice = Invoice(f, actor.Id, "LATER-PAYMENTS", InvoiceStatus.Pending);
        f.Db.AddRange(invoice, new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 5 });
        await f.Db.SaveChangesAsync();
        var confirmation = await client.GetStringAsync($"/Invoices/Confirm/{invoice.Id}?culture=en");
        var confirmationOperation = Guid.NewGuid();
        using (var response = await Post(client, invoice, WorkspaceUiTests.Token(confirmation), confirmationOperation, "2", "Cash", "Confirm"))
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        invoice = await f.Db.Invoices.AsNoTracking().SingleAsync();
        var form = await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}?culture=en");
        Assert.Contains("data-invoice-payment", form);
        Assert.Contains("data-modal-form", form);
        Assert.Contains("min=\"0.01\"", form);
        Assert.Equal(8m, decimal.Parse(System.Text.RegularExpressions.Regex.Match(form, "max=\"([^\"]+)\"")
            .Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        var token = WorkspaceUiTests.Token(form);
        foreach (var invalid in new[] { "0", "-1", "0.001", "8.01" })
        {
            using var rejected = await Post(client, invoice, token, Guid.NewGuid(), invalid);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        foreach (var invalidMethod in new[] { "", "987" })
        {
            using var rejected = await Post(client, invoice, token, Guid.NewGuid(), "1", invalidMethod);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        using (var reusedConfirmation = await Post(client, invoice, token, confirmationOperation, "2"))
            Assert.Equal(HttpStatusCode.Conflict, reusedConfirmation.StatusCode);
        var operation = Guid.NewGuid();
        var repeated = await Task.WhenAll(Post(client, invoice, token, operation, "2"), Post(client, invoice, token, operation, "2"));
        foreach (var response in repeated)
        {
            using (response) Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
        using (var changed = await Post(client, invoice, token, operation, "3"))
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using (var changedMethod = await Post(client, invoice, token, operation, "2", "Card"))
            Assert.Equal(HttpStatusCode.Conflict, changedMethod.StatusCode);
        f.Db.ChangeTracker.Clear();
        invoice = await f.Db.Invoices.AsNoTracking().SingleAsync();
        Assert.Equal(4m, invoice.PaidAmount);
        Assert.Equal(6m, invoice.RemainingAmount);
        Assert.Equal(PaymentStatus.Partial, invoice.PaymentStatus);
        var renamed = await f.Db.Users.SingleAsync(item => item.Id == actor.Id);
        renamed.FullName = "Updated display name";
        await f.Db.SaveChangesAsync();
        var finalForm = await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}?culture=en");
        using (var final = await Post(client, invoice, WorkspaceUiTests.Token(finalForm), Guid.NewGuid(), "6", "BankTransfer"))
            Assert.True(final.IsSuccessStatusCode, await final.Content.ReadAsStringAsync());
        // A retry of an already successful request is valid even after later payments settle the invoice.
        using (var retry = await Post(client, invoice, token, operation, "2"))
            Assert.True(retry.IsSuccessStatusCode, await retry.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        invoice = await f.Db.Invoices.AsNoTracking().SingleAsync();
        Assert.Equal(10m, invoice.PaidAmount);
        Assert.Equal(0m, invoice.RemainingAmount);
        Assert.Equal(PaymentStatus.Paid, invoice.PaymentStatus);
        Assert.Equal(InvoiceStatus.Confirmed, invoice.Status);
        var payments = await f.Db.Payments.OrderBy(item => item.Amount).ToListAsync();
        Assert.Equal(3, payments.Count);
        Assert.All(payments.Where(item => item.Amount == 2), item => Assert.Equal(name, item.RecordedByFullName));
        Assert.Equal("Updated display name", payments.Single(item => item.Amount == 6).RecordedByFullName);
        Assert.All(payments, item => Assert.Equal(actor.Id, item.CreatedBy));
        Assert.Single(await f.Db.InvoiceStatusHistory.ToListAsync());
        var stock = await f.Db.Stocks.SingleAsync();
        Assert.Equal(5m, stock.Quantity);
        Assert.Equal(2m, stock.ReservedQuantity);
        using (var unavailable = await client.GetAsync($"/Invoices/RecordPayment/{invoice.Id}"))
            Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        using (var overpaid = await Post(client, invoice, token, Guid.NewGuid(), "0.01"))
            Assert.Equal(HttpStatusCode.Conflict, overpaid.StatusCode);
        var details = await client.GetStringAsync($"/Invoices/Details/{invoice.Id}?culture=en");
        Assert.Contains("Payment history", details);
        Assert.Contains(name, details);
        Assert.Contains("Updated display name", details);
        Assert.DoesNotContain($"data-modal-url=\"/Invoices/RecordPayment/{invoice.Id}\"", details);
        var arabic = WebUtility.HtmlDecode(await client.GetStringAsync($"/Invoices/Details/{invoice.Id}?culture=ar"));
        Assert.Contains("سجل الدفعات", arabic);
        Assert.Contains("تحويل بنكي", arabic);
    }

    [PostgresFact]
    public async Task ConcurrentDistinctPaymentsRejectTheStaleFormAndAllowOtherInvoiceStates()
    {
        await using var f = await PostgresInventoryTests.Database.Create();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = Invoice(f, actor.Id, "CONCURRENT-PAYMENTS");
        f.Db.AddRange(invoice, new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 5, ReservedQuantity = 2 });
        await f.Db.SaveChangesAsync();
        var form = await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}");
        var token = WorkspaceUiTests.Token(form);
        // A zero-payment confirmation also owns its operation id, despite having no receipt.
        var confirmationOperation = Guid.NewGuid();
        f.Db.InvoiceStatusHistory.Add(new InvoiceStatusHistory
        {
            InvoiceId = invoice.Id, OperationId = confirmationOperation, ActorId = actor.Id,
            ActorFullName = actor.FullName, FromStatus = InvoiceStatus.Pending, ToStatus = InvoiceStatus.Confirmed
        });
        await f.Db.SaveChangesAsync();
        using (var reused = await Post(client, invoice, token, confirmationOperation, "1"))
            Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        var results = await Task.WhenAll(Post(client, invoice, token, Guid.NewGuid(), "7"), Post(client, invoice, token, Guid.NewGuid(), "7"));
        Assert.Single(results, response => response.IsSuccessStatusCode);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var result in results) result.Dispose();
        f.Db.ChangeTracker.Clear();
        Assert.Equal(7m, (await f.Db.Invoices.SingleAsync()).PaidAmount);
        Assert.Single(await f.Db.Payments.ToListAsync());
        Assert.Equal(2m, (await f.Db.Stocks.SingleAsync()).ReservedQuantity);
        foreach (var status in new[] { InvoiceStatus.Pending, InvoiceStatus.Draft })
        {
            var other = Invoice(f, actor.Id, status.ToString(), status);
            f.Db.Add(other);
            await f.Db.SaveChangesAsync();
            var otherForm = await client.GetStringAsync($"/Invoices/RecordPayment/{other.Id}");
            using var accepted = await Post(client, other, WorkspaceUiTests.Token(otherForm), Guid.NewGuid(), "1");
            Assert.True(accepted.IsSuccessStatusCode, await accepted.Content.ReadAsStringAsync());
        }
        using var noCsrf = await client.PostAsync($"/Invoices/RecordPayment/{invoice.Id}", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
    }

    [PostgresFact]
    public async Task PaymentStorageFailureRollsBackTotalsAndAllowsRetry()
    {
        await using var f = await PostgresInventoryTests.Database.Create();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = Invoice(f, actor.Id, "ROLLBACK-PAYMENT");
        f.Db.Add(invoice);
        await f.Db.SaveChangesAsync();
        await f.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_later_payment() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected later payment failure'; END $$;
            CREATE TRIGGER reject_later_payment BEFORE INSERT ON "Payments"
            FOR EACH ROW EXECUTE FUNCTION reject_later_payment();
            """);
        var form = await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}");
        var token = WorkspaceUiTests.Token(form);
        var operation = Guid.NewGuid();
        using (var failed = await Post(client, invoice, token, operation, "4"))
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        f.Db.ChangeTracker.Clear();
        var unchanged = await f.Db.Invoices.AsNoTracking().SingleAsync();
        Assert.Equal(0m, unchanged.PaidAmount);
        Assert.Equal(10m, unchanged.RemainingAmount);
        Assert.Equal(invoice.RowVersion, unchanged.RowVersion);
        Assert.Empty(await f.Db.Payments.ToListAsync());
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_later_payment ON \"Payments\";");
        using var retry = await Post(client, invoice, token, operation, "4");
        Assert.True(retry.IsSuccessStatusCode, await retry.Content.ReadAsStringAsync());
        Assert.Single(await f.Db.Payments.ToListAsync());
    }

    [PostgresFact]
    public async Task RecorderMigrationPreservesHistoricalConfirmationNamesAndAmounts()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true,
            migrationTarget: "20261119102650_AddInvoiceConfirmationHistory");
        var actor = new NEXT.Infrastructure.Identity.Entities.ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = "payment-migration", FullName = "Current name", BranchId = f.Branch
        };
        var invoice = Invoice(f, actor.Id, "HISTORY-MIGRATION");
        invoice.PaidAmount = 4; invoice.RemainingAmount = 6; invoice.PaymentStatus = PaymentStatus.Partial;
        var operation = Guid.NewGuid();
        invoice.Items = []; // Historical columns are seeded explicitly below, before the new line-source schema exists.
        f.Db.AddRange(actor, invoice);
        await f.Db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;
        var date = DateOnly.FromDateTime(now.DateTime);
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InvoiceItems" ("Id", "InvoiceId", "ProductId", "Quantity", "UnitPrice", "UnitCost", "DiscountAmount", "TaxRate", "TaxAmount", "TotalAmount", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {invoice.Id}, {f.Product}, 2, 5, 0, 0, 0, 0, 10, {now});
            INSERT INTO "InvoiceStatusHistory" ("Id", "InvoiceId", "OperationId", "ActorId", "ActorFullName",
                "FromStatus", "ToStatus", "PaymentAmount", "PaymentMethod", "ActionAt", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {invoice.Id}, {operation}, {actor.Id}, 'Original name', 0, 2, 4, 0, {now}, {now});
            INSERT INTO "Payments" ("Id", "PaymentNumber", "PaymentType", "InvoiceId", "Amount", "PaymentMethod",
                "PaymentDate", "RowVersion", "CreatedAt", "CreatedBy")
            VALUES ({operation}, 'HISTORICAL-PAYMENT', 0, {invoice.Id}, 4, 0, {date}, {Guid.NewGuid().ToByteArray()}, {now}, {actor.Id})
            """);
        await f.Db.Database.MigrateAsync();
        f.Db.ChangeTracker.Clear();
        var payment = await f.Db.Payments.SingleAsync();
        Assert.Equal("Original name", payment.RecordedByFullName);
        Assert.Equal(4m, payment.Amount);
        Assert.Equal(operation, payment.Id);
        Assert.Equal(4m, (await f.Db.Invoices.SingleAsync()).PaidAmount);
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }

    private static Invoice Invoice(PostgresInventoryTests.Database f, Guid actor, string number,
        InvoiceStatus status = InvoiceStatus.Confirmed) => new()
    {
        Id = Guid.NewGuid(), InvoiceNumber = number, CustomerName = "Payments customer",
        UserId = actor, WarehouseId = f.Source, Status = status, TotalAmount = 10, SubTotal = 10, RemainingAmount = 10,
        Items = [new InvoiceItem { ProductId = f.Product, Quantity = 2, UnitPrice = 5, TotalAmount = 10 }]
    };

    private static Task<HttpResponseMessage> Post(HttpClient client, Invoice invoice, string token, Guid operation,
        string amount, string method = "Cash", string action = "RecordPayment") =>
        WorkspaceUiTests.Post(client, $"/Invoices/{action}/{invoice.Id}", token,
            ("OperationId", operation.ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("Amount", amount), ("PaymentMethod", method));
}
