using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceRevisionTests
{
    public static IEnumerable<object[]> Statuses => Enum.GetValues<InvoiceStatus>().Where(status => status != InvoiceStatus.Cancelled).Select(status => new object[] { status });

    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task PaymentRemainsAvailableInEveryStatusUntilFullySettled(InvoiceStatus status)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var invoice = await Seed(db, status is InvoiceStatus.Shipped or InvoiceStatus.Delivered ? InvoiceStatus.Confirmed : status);
        if (status is InvoiceStatus.Shipped or InvoiceStatus.Delivered)
        {
            var product = new Product { Name = "Settled lifecycle product" };
            invoice.Items.Add(new InvoiceItem { Product = product, Quantity = 1, UnitPrice = 10, TotalAmount = 10 });
            db.Stocks.Add(new Stock { Product = product, WarehouseId = invoice.WarehouseId, Quantity = 1, ReservedQuantity = 1 });
            await db.SaveChangesAsync();
            var fulfillmentToken = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}"));
            using var dispatched = await WorkspaceUiTests.Post(client, $"/Invoices/Fulfill/{invoice.Id}", fulfillmentToken,
                ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
                ("TargetStatus", status.ToString()), ("CustomerPhone", "+249123456789"));
            Assert.True(dispatched.IsSuccessStatusCode, await dispatched.Content.ReadAsStringAsync());
            db.ChangeTracker.Clear();
            invoice = await db.Invoices.SingleAsync();
        }
        var button = $"data-modal-url=\"/Invoices/RecordPayment/{invoice.Id}\"";
        var historyBeforePayments = await db.InvoiceStatusHistory.CountAsync();
        Assert.Contains(button, await client.GetStringAsync($"/Invoices/Details/{invoice.Id}"));
        foreach (var amount in new[] { "3", "7" })
        {
            var form = await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}");
            using var response = await Post(client, invoice, WorkspaceUiTests.Token(form), Guid.NewGuid(), amount);
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            db.ChangeTracker.Clear();
            invoice = await db.Invoices.SingleAsync();
            Assert.Equal(status, invoice.Status);
            var details = await client.GetStringAsync($"/Invoices/Details/{invoice.Id}");
            if (invoice.RemainingAmount > 0) Assert.Contains(button, details);
            else Assert.DoesNotContain(button, details);
        }
        Assert.Equal(10m, invoice.PaidAmount);
        Assert.Equal(0m, invoice.RemainingAmount);
        Assert.Equal(PaymentStatus.Paid, invoice.PaymentStatus);
        Assert.Equal(2, await db.Payments.CountAsync());
        Assert.Equal(historyBeforePayments, await db.InvoiceStatusHistory.CountAsync());
        using var unavailable = await client.GetAsync($"/Invoices/RecordPayment/{invoice.Id}");
        Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
    }

    [Theory]
    [InlineData("0", 3)]
    [InlineData("2", 5)]
    [InlineData("7", 10)]
    public async Task ConfirmingAPrepaidInvoiceKeepsReceiptsAndAddsOnlyTheNewPayment(string additional, decimal expected)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var invoice = await Seed(db, InvoiceStatus.Pending);
        var product = new Product { Name = "Prepaid product", IsActive = true };
        invoice.Items.Add(new InvoiceItem { Product = product, Quantity = 2, UnitPrice = 5, TotalAmount = 10 });
        db.Stocks.Add(new Stock { Product = product, WarehouseId = invoice.WarehouseId, Quantity = 5 });
        await db.SaveChangesAsync();
        var paymentForm = await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}");
        var paymentOperation = Guid.NewGuid();
        using (var payment = await Post(client, invoice, WorkspaceUiTests.Token(paymentForm), paymentOperation, "3"))
            Assert.True(payment.IsSuccessStatusCode, await payment.Content.ReadAsStringAsync());
        db.ChangeTracker.Clear();
        invoice = await db.Invoices.SingleAsync();
        var form = await client.GetStringAsync($"/Invoices/Confirm/{invoice.Id}");
        // A receipt operation cannot be reused as a confirmation, even when it adds no money.
        using (var collision = await Post(client, invoice, WorkspaceUiTests.Token(form), paymentOperation, "0", "Confirm"))
            Assert.Equal(HttpStatusCode.Conflict, collision.StatusCode);
        var operation = Guid.NewGuid();
        using (var confirmation = await Post(client, invoice, WorkspaceUiTests.Token(form), operation, additional, "Confirm"))
            Assert.True(confirmation.IsSuccessStatusCode, await confirmation.Content.ReadAsStringAsync());
        using (var retry = await Post(client, invoice, WorkspaceUiTests.Token(form), operation, additional, "Confirm"))
            Assert.True(retry.IsSuccessStatusCode, await retry.Content.ReadAsStringAsync());
        db.ChangeTracker.Clear();
        invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatus.Confirmed, invoice.Status);
        Assert.Equal(expected, invoice.PaidAmount);
        Assert.Equal(10 - expected, invoice.RemainingAmount);
        Assert.Equal(expected == 10 ? PaymentStatus.Paid : PaymentStatus.Partial, invoice.PaymentStatus);
        Assert.Equal(expected, await db.Payments.SumAsync(item => item.Amount));
        Assert.Equal(additional == "0" ? 1 : 2, await db.Payments.CountAsync());
        var stock = await db.Stocks.SingleAsync();
        Assert.Equal(5m, stock.Quantity);
        Assert.Equal(2m, stock.ReservedQuantity);
        Assert.Single(await db.InvoiceStatusHistory.ToListAsync());
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    public async Task NotesAppearSafelyInDetailsPreviewAndPrintOnlyWhenPresent(string culture)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var invoice = await Seed(db, InvoiceStatus.Pending);
        invoice.Notes = "ملاحظة الفاتورة\n<script>alert('note')</script>";
        await db.SaveChangesAsync();
        foreach (var route in new[] { "Invoices/Details", "InvoiceDocuments/Preview", "InvoiceDocuments/Print" })
        {
            var html = await client.GetStringAsync($"/{route}/{invoice.Id}?culture={culture}");
            Assert.Contains("invoice-document-notes", html);
            Assert.Contains(invoice.Notes, WebUtility.HtmlDecode(html));
            Assert.DoesNotContain("<script>alert('note')</script>", html);
        }
        invoice.Notes = "  ";
        await db.SaveChangesAsync();
        var preview = await client.GetStringAsync($"/InvoiceDocuments/Preview/{invoice.Id}?culture={culture}");
        Assert.DoesNotContain("<section class=\"invoice-document-notes\">", preview);
    }

    [Fact]
    public async Task LegacyAccountingHasNoRoutesAndInvoicesNeedNoFinancialSettings()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        foreach (var action in new[] { "Index", "Settings", "Review", "Details", "Configure", "Initialize" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/CustomerAccounts/{action}")).StatusCode);
        var home = await client.GetStringAsync("/");
        Assert.DoesNotContain("href=\"/CustomerAccounts", home);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity => entity.ClrType.Name is "JournalEntry" or "JournalLine");
        Assert.Equal(15, await db.Accounts.CountAsync());
        Assert.False(await db.SystemSettings.AnyAsync(item => item.Key == "Accounting.InvoiceAccounts"));
    }

    [PostgresFact]
    public async Task AccountingRemovalMigrationKeepsInvoicesReceiptsAndCustomers()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true,
            migrationTarget: "20261120192020_AddCustomerInvoiceAccounts");
        var actor = new NEXT.Infrastructure.Identity.Entities.ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = "removal-migration", FullName = "Migration operator", BranchId = f.Branch
        };
        var customer = new Customer { Name = "Existing customer", CurrentBalance = 6 };
        var invoice = new Invoice
        {
            InvoiceNumber = "REMOVAL-MIGRATION", UserId = actor.Id, WarehouseId = f.Source, Customer = customer,
            Status = InvoiceStatus.Delivered, TotalAmount = 10, PaidAmount = 4, RemainingAmount = 6,
            PaymentStatus = PaymentStatus.Partial, Notes = "Preserved invoice note"
        };
        var receipt = new Payment
        {
            Id = Guid.NewGuid(),
            PaymentNumber = "REMOVAL-RECEIPT", Invoice = invoice, Customer = customer,
            Amount = 4, PaymentType = PaymentType.Receipt, RecordedByFullName = actor.FullName
        };
        f.Db.AddRange(actor, invoice, new SystemSetting { Key = "Accounting.InvoiceAccounts", Category = "Finance" });
        await f.Db.SaveChangesAsync();
        var accountId = Guid.NewGuid();
        var journalId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var date = DateOnly.FromDateTime(now.DateTime);
        var version = Guid.NewGuid().ToByteArray();
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Payments" ("Id", "PaymentNumber", "PaymentType", "InvoiceId", "CustomerId", "Amount",
                "PaymentMethod", "PaymentDate", "RecordedByFullName", "RowVersion", "CreatedAt")
            VALUES ({receipt.Id}, 'REMOVAL-RECEIPT', 0, {invoice.Id}, {customer.Id}, 4, 0, {date}, {actor.FullName}, {version}, {now});
            INSERT INTO "Accounts" ("Id", "Name", "Code", "AccountType", "Balance", "IsActive", "CreatedAt")
            VALUES ({accountId}, 'Existing account', 'EXISTING', 0, 6, true, {now});
            UPDATE "Customers" SET "AccountId" = {accountId} WHERE "Id" = {customer.Id};
            INSERT INTO "JournalEntries" ("Id", "EntryNumber", "EntryDate", "ReferenceType", "ReferenceId",
                "TotalDebit", "TotalCredit", "RowVersion", "CreatedAt")
            VALUES ({journalId}, 'EXISTING-JOURNAL', {date}, 'Invoice', {invoice.Id}, 6, 0, {version}, {now});
            INSERT INTO "JournalLines" ("Id", "JournalEntryId", "AccountId", "Debit", "Credit", "CreatedAt")
            VALUES ({lineId}, {journalId}, {accountId}, 6, 0, {now});
            """);
        await f.Db.GetService<IMigrator>()
            .MigrateAsync("20261121093105_RemoveAccountingSystem");
        f.Db.ChangeTracker.Clear();
        var remainingTables = await f.Db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name IN ('Accounts', 'JournalEntries', 'JournalLines')
            """).SingleAsync();
        Assert.Equal(0, remainingTables);
        await f.Db.Database.MigrateAsync();
        Assert.False(await f.Db.SystemSettings.AnyAsync(item => item.Key == "Accounting.InvoiceAccounts"));
        var saved = await f.Db.Invoices.SingleAsync();
        Assert.Equal(invoice.RowVersion, saved.RowVersion);
        Assert.Equal("Preserved invoice note", saved.Notes);
        Assert.Equal(4m, saved.PaidAmount);
        Assert.Equal(6m, saved.RemainingAmount);
        Assert.Equal(InvoiceStatus.Delivered, saved.Status);
        Assert.Equal(receipt.Id, (await f.Db.Payments.SingleAsync()).Id);
        Assert.Equal(4m, (await f.Db.Payments.SingleAsync()).Amount);
        Assert.Equal(customer.Id, (await f.Db.Customers.SingleAsync()).Id);
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }

    private static async Task<Invoice> Seed(NEXTDbContext db, InvoiceStatus status)
    {
        var actor = await db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = new Invoice
        {
            InvoiceNumber = "INVOICE-REVISION", UserId = actor.Id, CustomerName = "Invoice customer",
            Warehouse = new Warehouse { Name = "Invoice warehouse", Code = "REVISION", IsActive = true },
            Status = status, TotalAmount = 10, SubTotal = 10, RemainingAmount = 10
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, Invoice invoice, string token, Guid operation,
        string amount, string action = "RecordPayment") => WorkspaceUiTests.Post(client, $"/Invoices/{action}/{invoice.Id}", token,
            ("OperationId", operation.ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("Amount", amount), ("PaymentMethod", "Cash"));
}
