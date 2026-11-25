using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceCancellationTests
{
    [PostgresFact]
    public async Task PendingEditsPreservePricesMergeLinesAndProtectReceiptsAndConcurrentChanges()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true, withReceiptRate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var invoice = await Seed(f);
        var originalNumber = invoice.InvoiceNumber;
        var form = await client.GetStringAsync($"/Invoices/Edit/{invoice.Id}");
        Assert.Contains("data-invoice-initial-lines", form);
        var token = WorkspaceUiTests.Token(form);
        var operation = Guid.NewGuid();
        async Task<HttpResponseMessage> Edit(Invoice value, Guid op, string quantity = "1", string notes = "Updated notes", string name = "Cancellation customer") =>
            await WorkspaceUiTests.Post(client, $"/Invoices/Edit/{value.Id}", token,
                ("OperationId", op.ToString()), ("RowVersion", Convert.ToBase64String(value.RowVersion)),
                ("CustomerName", name), ("CustomerId", value.CustomerId?.ToString() ?? ""), ("Notes", notes), ("DiscountType", "FixedAmount"), ("DiscountValue", "1"),
                ("Lines[0].ProductId", f.Product.ToString()), ("Lines[0].Quantity", quantity), ("Lines[0].QuotedUnitPrice", "5"),
                ("Lines[1].ProductId", f.Product.ToString()), ("Lines[1].Quantity", "2"), ("Lines[1].QuotedUnitPrice", "5"));
        using (var edited = await Edit(invoice, operation)) await Success(edited);
        using (var retry = await Edit(invoice, operation)) await Success(retry);
        using (var mismatch = await Edit(invoice, operation, notes: "Changed request")) Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        using (var stale = await Edit(invoice, Guid.NewGuid())) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var saved = await Reload(f);
        Assert.Equal(originalNumber, saved.InvoiceNumber);
        Assert.Equal(14m, saved.TotalAmount);
        Assert.Equal(3m, Assert.Single(saved.Items).Quantity);
        Assert.Equal(5m, saved.Items.Single().UnitPrice); // Current catalog price is 4; editing does not silently reprice it.
        Assert.Equal("Updated notes", saved.Notes);
        Assert.Single(await f.Db.InvoiceStatusHistory.ToListAsync());
        Assert.Equal(0m, (await f.Db.Stocks.SingleAsync()).ReservedQuantity);
        var payToken = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}"));
        using (var paid = await Money(client, saved, payToken, Guid.NewGuid(), "12")) await Success(paid);
        saved = await Reload(f);
        var paidCustomerId = saved.CustomerId;
        Assert.NotNull(paidCustomerId);
        f.Db.Customers.Add(new Customer { Name = "Cancellation customer" });
        await f.Db.SaveChangesAsync();
        using (var prepaidEdit = await Edit(saved, Guid.NewGuid(), notes: "Prepaid edit")) await Success(prepaidEdit);
        saved = await Reload(f);
        Assert.Equal(paidCustomerId, saved.CustomerId); // A duplicate name must not reassign posted receipts.
        using (var belowPaid = await Edit(saved, Guid.NewGuid(), quantity: "0.1")) Assert.Equal(HttpStatusCode.BadRequest, belowPaid.StatusCode);
        using (var otherCustomer = await Edit(saved, Guid.NewGuid(), name: "Another customer")) Assert.Equal(HttpStatusCode.BadRequest, otherCustomer.StatusCode);
        var confirmToken = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Confirm/{invoice.Id}"));
        using (var confirmed = await Money(client, saved, confirmToken, Guid.NewGuid(), "0", action: "Confirm")) await Success(confirmed);
        saved = await Reload(f);
        using (var unavailable = await client.GetAsync($"/Invoices/Edit/{invoice.Id}")) Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        using (var unavailable = await Edit(saved, Guid.NewGuid())) Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        Assert.Equal(14m, (await Reload(f)).TotalAmount);
    }

    [PostgresFact]
    public async Task CancellationRetainsCustomerCreditAndRefundsAreSeparatePartialIdempotentTreasuryPayments()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var invoice = await Seed(f);
        var confirmToken = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Confirm/{invoice.Id}"));
        using (var confirmed = await Money(client, invoice, confirmToken, Guid.NewGuid(), "3", action: "Confirm")) await Success(confirmed);
        invoice = await Reload(f);
        Assert.Equal(2m, (await f.Db.Stocks.SingleAsync()).ReservedQuantity);
        var token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Cancel/{invoice.Id}"));
        var cancellation = Guid.NewGuid();
        using (var cancelled = await Cancel(client, invoice, token, cancellation)) await Success(cancelled);
        using (var retry = await Cancel(client, invoice, token, cancellation)) await Success(retry);
        using (var mismatch = await Cancel(client, invoice, token, cancellation, "Different reason")) Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        invoice = await Reload(f);
        Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal(0m, invoice.RemainingAmount);
        Assert.NotNull(invoice.CustomerId);
        Assert.Equal("Cancellation customer", (await f.Db.Customers.SingleAsync(item => item.Id == invoice.CustomerId)).Name);
        Assert.Equal(20m, (await f.Db.Stocks.SingleAsync()).Quantity);
        Assert.Equal(0m, (await f.Db.Stocks.SingleAsync()).ReservedQuantity);
        Assert.Empty(await f.Db.StockMovements.ToListAsync());
        Assert.Equal(3m, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(0m, await Net(f.Db, AccountType.CustomerAdvances));
        Assert.Equal(-3m, await Net(f.Db, AccountType.CustomerRefunds));
        Assert.Equal(0m, await Net(f.Db, AccountType.Sales));
        Assert.Equal(1, await f.Db.Payments.CountAsync());
        await AssertSqlCannotOverRefund(f, invoice);
        var details = await client.GetStringAsync($"/Invoices/Details/{invoice.Id}");
        Assert.DoesNotContain("data-modal-url=\"/Invoices/RecordPayment", details);
        Assert.Contains("data-modal-url=\"/Invoices/Refund", details);
        var refundToken = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Refund/{invoice.Id}"));
        using (var invalid = await Money(client, invoice, refundToken, Guid.NewGuid(), "4", action: "Refund")) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var missingMethod = await Money(client, invoice, refundToken, Guid.NewGuid(), "1", "", "Refund")) Assert.Equal(HttpStatusCode.BadRequest, missingMethod.StatusCode);
        var operation = Guid.NewGuid();
        using (var partial = await Money(client, invoice, refundToken, operation, "1", "BankTransfer", "Refund")) await Success(partial);
        using (var retry = await Money(client, invoice, refundToken, operation, "1", "BankTransfer", "Refund")) await Success(retry);
        using (var changed = await Money(client, invoice, refundToken, operation, "1", "Cash", "Refund")) Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using (var stale = await Money(client, invoice, refundToken, Guid.NewGuid(), "1", action: "Refund")) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        invoice = await Reload(f);
        Assert.Equal(2m, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(-2m, await Net(f.Db, AccountType.CustomerRefunds));
        var finalOperation = Guid.NewGuid();
        using (var final = await Money(client, invoice, refundToken, finalOperation, "2", "Wallet", "Refund")) await Success(final);
        using (var retry = await Money(client, invoice, refundToken, finalOperation, "2", "Wallet", "Refund")) await Success(retry);
        Assert.Equal(0m, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(0m, await Net(f.Db, AccountType.CustomerRefunds));
        Assert.Equal(2, await f.Db.Payments.CountAsync(item => item.Purpose == PaymentPurpose.CustomerRefund));
        using (var unavailable = await client.GetAsync($"/Invoices/Refund/{invoice.Id}")) Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        using (var cancelledPayment = await Money(client, await Reload(f), refundToken, Guid.NewGuid(), "1")) Assert.Equal(HttpStatusCode.Conflict, cancelledPayment.StatusCode);
    }

    [PostgresFact]
    public async Task CancellationAndRefundPostingFailuresRollBackReservationCreditPaymentAndVersion()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var invoice = await Seed(f);
        var token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Confirm/{invoice.Id}"));
        using (var confirmed = await Money(client, invoice, token, Guid.NewGuid(), "3", action: "Confirm")) await Success(confirmed);
        invoice = await Reload(f);
        await f.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_invoice_refund() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW."Kind" IN (5, 7) THEN RAISE EXCEPTION 'Injected cancellation/refund failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_invoice_refund BEFORE INSERT ON "LedgerEntries" FOR EACH ROW EXECUTE FUNCTION reject_invoice_refund();
            """);
        var cancelToken = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Cancel/{invoice.Id}"));
        var cancellation = Guid.NewGuid();
        using (var failed = await Cancel(client, invoice, cancelToken, cancellation)) Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        var unchanged = await Reload(f);
        Assert.Equal(invoice.RowVersion, unchanged.RowVersion);
        Assert.Equal(InvoiceStatus.Confirmed, unchanged.Status);
        Assert.Equal(2m, (await f.Db.Stocks.SingleAsync()).ReservedQuantity);
        Assert.Equal(1, await f.Db.InvoiceStatusHistory.CountAsync());
        Assert.Equal(1, await f.Db.LedgerEntries.CountAsync());
        await f.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"LedgerEntries\" DISABLE TRIGGER reject_invoice_refund;");
        using (var retry = await Cancel(client, invoice, cancelToken, cancellation)) await Success(retry);
        invoice = await Reload(f);
        await f.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"LedgerEntries\" ENABLE TRIGGER reject_invoice_refund;");
        var refundToken = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Refund/{invoice.Id}"));
        var operation = Guid.NewGuid();
        using (var failed = await Money(client, invoice, refundToken, operation, "2", action: "Refund")) Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        unchanged = await Reload(f);
        Assert.Equal(invoice.RowVersion, unchanged.RowVersion);
        Assert.Equal(1, await f.Db.Payments.CountAsync());
        Assert.Equal(3m, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(-3m, await Net(f.Db, AccountType.CustomerRefunds));
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_invoice_refund ON \"LedgerEntries\";");
        using (var retry = await Money(client, invoice, refundToken, operation, "2", action: "Refund")) await Success(retry);
        Assert.Equal(1m, await Net(f.Db, AccountType.Treasury));
    }

    [PostgresFact]
    public async Task PendingCancellationAndConcurrentRefundsDoNotCreateDuplicateMoneyOrReservations()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var invoice = await Seed(f);
        var token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Cancel/{invoice.Id}"));
        using (var blank = await Cancel(client, invoice, token, Guid.NewGuid(), "")) Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        // A pending invoice may already have receipts; cancellation must not release another invoice's reserved stock.
        (await f.Db.Stocks.SingleAsync()).ReservedQuantity = 4;
        await f.Db.SaveChangesAsync();
        using (var paid = await Money(client, invoice, token, Guid.NewGuid(), "3")) await Success(paid);
        invoice = await Reload(f);
        using (var cancelled = await Cancel(client, invoice, token, Guid.NewGuid())) await Success(cancelled);
        invoice = await Reload(f);
        Assert.Equal(4m, (await f.Db.Stocks.SingleAsync()).ReservedQuantity);
        var results = await Task.WhenAll(Money(client, invoice, token, Guid.NewGuid(), "2", action: "Refund"),
            Money(client, invoice, token, Guid.NewGuid(), "2", action: "Refund"));
        try
        {
            Assert.Single(results, item => item.IsSuccessStatusCode);
            Assert.Single(results, item => item.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
        }
        finally { foreach (var response in results) response.Dispose(); }
        Assert.Equal(1m, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(-1m, await Net(f.Db, AccountType.CustomerRefunds));
        Assert.Equal(1, await f.Db.Payments.CountAsync(item => item.PaymentType == PaymentType.Payment));
    }

    [Fact]
    public async Task CancelledInvoicesRejectPaymentsAndShippedInvoicesRejectDirectCancellation()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var actor = await db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = new Invoice { InvoiceNumber = "BLOCKED-CANCEL", UserId = actor.Id, CustomerName = "Cancelled customer",
            Warehouse = new Warehouse { Name = "Cancellation warehouse", Code = "CANCEL" }, Status = InvoiceStatus.Cancelled,
            TotalAmount = 10, RemainingAmount = 10 };
        db.Add(invoice); await db.SaveChangesAsync();
        var token = WorkspaceUiTests.Token(await client.GetStringAsync("/Invoices/Create"));
        using (var paid = await Money(client, invoice, token, Guid.NewGuid(), "1")) Assert.Equal(HttpStatusCode.Conflict, paid.StatusCode);
        invoice.Status = InvoiceStatus.Shipped; await db.SaveChangesAsync();
        using (var cancel = await Cancel(client, invoice, token, Guid.NewGuid())) Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [PostgresFact]
    public async Task UnpaidCancellationCreatesNoFinancialMovementAndCannotBeConfirmedByReusingItsOperation()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var invoice = await Seed(f);
        var token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Cancel/{invoice.Id}"));
        var operation = Guid.NewGuid();
        using (var cancelled = await Cancel(client, invoice, token, operation)) await Success(cancelled);
        using (var wrongRetry = await Money(client, invoice, token, operation, "0", action: "Confirm"))
            Assert.Equal(HttpStatusCode.Conflict, wrongRetry.StatusCode);
        Assert.Equal(InvoiceStatus.Cancelled, (await Reload(f)).Status);
        Assert.Empty(await f.Db.Payments.ToListAsync());
        Assert.Empty(await f.Db.LedgerEntries.ToListAsync());
        Assert.Equal(0m, (await f.Db.Stocks.SingleAsync()).ReservedQuantity);
        using var refund = await client.GetAsync($"/Invoices/Refund/{invoice.Id}");
        Assert.Equal(HttpStatusCode.Conflict, refund.StatusCode);
    }

    [PostgresFact]
    public async Task CancellationMigrationUpgradesExistingSchemaAndCanBeRepeatedOrRevertedBeforeUse()
    {
        const string previous = "20261121105922_AddAccountingFoundation";
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true, migrationTarget: previous);
        var migrator = f.Db.GetService<IMigrator>();
        var script = migrator.GenerateScript(previous, null, MigrationsSqlGenerationOptions.Idempotent);
        await f.Db.Database.ExecuteSqlRawAsync(script);
        await f.Db.Database.ExecuteSqlRawAsync(script);
        Assert.Equal(12, await f.Db.Accounts.CountAsync());
        await migrator.MigrateAsync(previous);
        Assert.Equal(7, await f.Db.Accounts.CountAsync());
        await f.Db.Database.ExecuteSqlRawAsync(script);
        Assert.Equal(12, await f.Db.Accounts.CountAsync());
        var error = await Assert.ThrowsAsync<PostgresException>(() => f.Db.Database.ExecuteSqlRawAsync("UPDATE \"Accounts\" SET \"Name\" = 'Changed' WHERE \"Type\" = 8"));
        Assert.Equal("23514", error.SqlState);
    }

    private static async Task AssertSqlCannotOverRefund(PostgresInventoryTests.Database f, Invoice invoice)
    {
        await using var db = f.NewContext();
        var entry = Guid.NewGuid(); var payment = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var operation = Guid.NewGuid(); var actor = invoice.UserId; var customer = invoice.CustomerId!.Value;
        var cash = Account.IdFor(AccountType.Treasury); var refunds = Account.IdFor(AccountType.CustomerRefunds);
        var key = LedgerEntry.Key(LedgerEntryKind.Refund, AccountingReferenceType.Payment, payment);
        var now = DateTimeOffset.UtcNow; var date = DateOnly.FromDateTime(now.DateTime);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Payments" ("Id", "PaymentNumber", "PaymentType", "Purpose", "InvoiceId", "CustomerId", "Amount", "PaymentMethod", "PaymentDate", "CreatedAt", "RowVersion")
                VALUES ({payment}, {"INVALID-REFUND"}, 1, 3, {invoice.Id}, {customer}, 4, 0, {date}, {now}, {new byte[] { 1 }});
            INSERT INTO "LedgerEntries" ("Id", "OperationId", "PostingKey", "RequestHash", "Kind", "ReferenceType", "ReferenceId", "InvoiceId", "PaymentId", "EntryDate", "Description", "ActorId", "ActorName", "CreatedAt")
                VALUES ({entry}, {operation}, {key}, {new string('A', 64)}, 5, 2, {payment}, {invoice.Id}, {payment}, {date}, 'Invalid refund', {actor}, 'Operator', {now});
            INSERT INTO "AccountTransactions" ("Id", "LedgerEntryId", "Sequence", "AccountId", "Debit", "Credit", "CustomerId")
                VALUES ({first}, {entry}, 1, {refunds}, 4, 0, {customer}), ({second}, {entry}, 2, {cash}, 0, 4, {customer});
            UPDATE "LedgerEntries" SET "PostedAt" = {now} WHERE "Id" = {entry};
            """);
        var error = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
        Assert.Equal("23514", error.SqlState);
        Assert.Contains("InvoiceRefundInvalid", error.MessageText);
    }

    private static async Task<Invoice> Seed(PostgresInventoryTests.Database f)
    {
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = new Invoice { InvoiceNumber = "CANCEL-" + Guid.NewGuid().ToString("N"), UserId = actor.Id,
            WarehouseId = f.Source, CustomerName = "Cancellation customer", SubTotal = 10, TotalAmount = 10, RemainingAmount = 10,
            Items = [new InvoiceItem { ProductId = f.Product, Quantity = 2, UnitPrice = 5, UnitCost = 4, TotalAmount = 10 }] };
        (await f.Db.Warehouses.SingleAsync(item => item.Id == f.Source)).IsDefault = true;
        f.Db.AddRange(invoice, new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 20 });
        await f.Db.SaveChangesAsync();
        return invoice;
    }

    private static async Task<Invoice> Reload(PostgresInventoryTests.Database f)
    { f.Db.ChangeTracker.Clear(); return await f.Db.Invoices.AsNoTracking().Include(item => item.Items).SingleAsync(); }
    private static async Task<decimal> Net(NEXTDbContext db, AccountType type) => await db.AccountTransactions
        .Where(line => line.AccountId == Account.IdFor(type) && line.LedgerEntry.PostedAt != null).SumAsync(line => line.Debit - line.Credit);
    private static async Task Success(HttpResponseMessage response) => Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    private static Task<HttpResponseMessage> Cancel(HttpClient client, Invoice invoice, string token, Guid operation, string reason = "Customer cancelled") =>
        WorkspaceUiTests.Post(client, $"/Invoices/Cancel/{invoice.Id}", token, ("OperationId", operation.ToString()),
            ("RowVersion", Convert.ToBase64String(invoice.RowVersion)), ("Reason", reason));
    private static Task<HttpResponseMessage> Money(HttpClient client, Invoice invoice, string token, Guid operation,
        string amount, string method = "Cash", string action = "RecordPayment") =>
        WorkspaceUiTests.Post(client, $"/Invoices/{action}/{invoice.Id}", token, ("OperationId", operation.ToString()),
            ("RowVersion", Convert.ToBase64String(invoice.RowVersion)), ("Amount", amount), ("PaymentMethod", method));
}
