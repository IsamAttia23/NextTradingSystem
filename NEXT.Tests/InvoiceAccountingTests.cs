using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Services;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceAccountingTests
{
    [PostgresFact]
    public async Task PartialAdvanceDispatchLaterReceiptsAndDeliveryPostExactlyOnceWithInvoiceLinks()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = await Seed(f, actor.Id, InvoiceStatus.Pending);
        var confirmForm = await client.GetStringAsync($"/Invoices/Confirm/{invoice.Id}");
        var confirmationOperation = Guid.NewGuid();
        using (var confirmed = await Pay(client, invoice, WorkspaceUiTests.Token(confirmForm), confirmationOperation, "3", "Cash", "Confirm"))
            Assert.True(confirmed.IsSuccessStatusCode, await confirmed.Content.ReadAsStringAsync());
        Assert.Equal(3m, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(-3m, await Net(f.Db, AccountType.CustomerAdvances));
        Assert.Equal(0m, await Net(f.Db, AccountType.Sales));
        f.Db.ChangeTracker.Clear();
        invoice = await f.Db.Invoices.SingleAsync();
        var form = await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}");
        Assert.Contains("name=\"CustomerPhone\"", form);
        var operation = Guid.NewGuid();
        using (var shipped = await Fulfill(client, invoice, WorkspaceUiTests.Token(form), operation, InvoiceStatus.Shipped, "+249123456789"))
            Assert.True(shipped.IsSuccessStatusCode, await shipped.Content.ReadAsStringAsync());
        using (var retry = await Fulfill(client, invoice, WorkspaceUiTests.Token(form), operation, InvoiceStatus.Shipped, "+249123456789"))
            Assert.True(retry.IsSuccessStatusCode, await retry.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        invoice = await f.Db.Invoices.SingleAsync();
        Assert.NotNull(invoice.CustomerId);
        Assert.Equal("+249123456789", (await f.Db.Customers.SingleAsync()).Phone);
        Assert.Equal(1, await f.Db.CustomerDebtAccounts.CountAsync());
        Assert.Equal(7m, (await f.Db.DebtItems.SingleAsync()).OriginalAmount);
        Assert.Equal(7m, await Net(f.Db, AccountType.Receivables));
        Assert.Equal(0m, await Net(f.Db, AccountType.CustomerAdvances));
        Assert.Equal(-10m, await Net(f.Db, AccountType.Sales));
        var paymentForm = await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}");
        var paymentOperation = Guid.NewGuid();
        using (var paid = await Pay(client, invoice, WorkspaceUiTests.Token(paymentForm), paymentOperation, "2", "Wallet"))
            Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());
        using (var retry = await Pay(client, invoice, WorkspaceUiTests.Token(paymentForm), paymentOperation, "2", "Wallet"))
            Assert.True(retry.IsSuccessStatusCode, await retry.Content.ReadAsStringAsync());
        Assert.Equal(5m, await Net(f.Db, AccountType.Receivables));
        Assert.Equal(5m, await Net(f.Db, AccountType.Treasury));
        f.Db.ChangeTracker.Clear();
        invoice = await f.Db.Invoices.SingleAsync();
        var deliveryForm = await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}");
        using (var delivered = await Fulfill(client, invoice, WorkspaceUiTests.Token(deliveryForm), Guid.NewGuid(), InvoiceStatus.Delivered))
            Assert.True(delivered.IsSuccessStatusCode, await delivered.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        invoice = await f.Db.Invoices.SingleAsync();
        var finalForm = await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}");
        using (var paid = await Pay(client, invoice, WorkspaceUiTests.Token(finalForm), Guid.NewGuid(), "5", "BankTransfer"))
            Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());
        Assert.Equal(0m, await Net(f.Db, AccountType.Receivables));
        Assert.Equal(10m, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(-10m, await Net(f.Db, AccountType.Sales));
        Assert.Equal(1, await f.Db.LedgerEntries.CountAsync(item => item.Kind == LedgerEntryKind.SalesRecognition));
        Assert.Equal(3, await f.Db.LedgerEntries.CountAsync(item => item.Kind == LedgerEntryKind.Receipt));
        Assert.Equal(3, await f.Db.Payments.CountAsync());
        Assert.All(await f.Db.Payments.ToListAsync(), item => Assert.Equal(PaymentPurpose.InvoiceReceipt, item.Purpose));
        var cashPage = WebUtility.HtmlDecode(await client.GetStringAsync("/FinancialAccounts/Movements?Account=Treasury&culture=ar"));
        Assert.Contains($"href=\"/Invoices/Details/{invoice.Id}\"", cashPage);
        Assert.Contains(invoice.InvoiceNumber, cashPage);
        Assert.Contains("سداد فاتورة", cashPage);
        Assert.Contains("محفظة", cashPage);
        var customerSearch = await client.GetStringAsync("/FinancialAccounts/Movements?Account=Treasury&SearchTerm=" +
            Uri.EscapeDataString("Accounting integration customer"));
        Assert.Contains(invoice.InvoiceNumber, customerSearch);
        var receiptNumber = "PAY-" + paymentOperation.ToString("N");
        var receiptSearch = await client.GetStringAsync("/FinancialAccounts/Movements?Account=Treasury&SearchTerm=" + receiptNumber);
        Assert.Contains($"href=\"/Invoices/Details/{invoice.Id}\"", receiptSearch);
        Assert.Contains(receiptNumber, receiptSearch);
        var customerFilter = await client.GetStringAsync($"/FinancialAccounts/Movements?Account=Treasury&CustomerId={invoice.CustomerId}");
        Assert.Contains("PAY-" + confirmationOperation.ToString("N"), customerFilter);
        var summary = WebUtility.HtmlDecode(await client.GetStringAsync("/FinancialAccounts?culture=ar"));
        Assert.Contains("الرصيد الختامي", summary);
        Assert.DoesNotContain("data-modal-url=\"/FinancialAccounts/Create", summary);
    }

    [PostgresFact]
    public async Task FullyPaidCounterSaleNeedsNoDebtOrPhoneAndDirectDeliveryRecognizesRevenueOnce()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = await Seed(f, actor.Id, InvoiceStatus.Pending);
        var form = await client.GetStringAsync($"/Invoices/Confirm/{invoice.Id}");
        using (var paid = await Pay(client, invoice, WorkspaceUiTests.Token(form), Guid.NewGuid(), "10", "Cash", "Confirm"))
            Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        invoice = await f.Db.Invoices.SingleAsync();
        form = await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}");
        Assert.DoesNotContain("name=\"CustomerPhone\"", form);
        using (var delivered = await Fulfill(client, invoice, WorkspaceUiTests.Token(form), Guid.NewGuid(), InvoiceStatus.Delivered))
            Assert.True(delivered.IsSuccessStatusCode, await delivered.Content.ReadAsStringAsync());
        Assert.Single(await f.Db.Customers.ToListAsync());
        Assert.NotNull(invoice.CustomerId);
        Assert.Empty(await f.Db.CustomerDebtAccounts.ToListAsync());
        Assert.Empty(await f.Db.DebtItems.ToListAsync());
        Assert.Equal(-10m, await Net(f.Db, AccountType.Sales));
        Assert.Equal(10m, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(0m, await Net(f.Db, AccountType.CustomerAdvances));
        Assert.Equal(2, await f.Db.LedgerEntries.CountAsync());
    }

    [PostgresFact]
    public async Task MissingPhoneAndPostingFailureRollBackCustomerDebtStockHistoryAndInvoiceThenAllowRetry()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = await Seed(f, actor.Id, InvoiceStatus.Confirmed);
        var form = await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}");
        var token = WorkspaceUiTests.Token(form);
        using (var missing = await Fulfill(client, invoice, token, Guid.NewGuid(), InvoiceStatus.Shipped))
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await AssertUnchanged();
        await f.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fail_invoice_accounting() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected invoice accounting failure'; END $$;
            CREATE TRIGGER test_fail_invoice_accounting BEFORE UPDATE ON "LedgerEntries"
            FOR EACH ROW EXECUTE FUNCTION fail_invoice_accounting();
            """);
        var operation = Guid.NewGuid();
        using (var failed = await Fulfill(client, invoice, token, operation, InvoiceStatus.Shipped, "+249123456789"))
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await AssertUnchanged();
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_fail_invoice_accounting ON \"LedgerEntries\";");
        using (var retry = await Fulfill(client, invoice, token, operation, InvoiceStatus.Shipped, "+249123456789"))
            Assert.True(retry.IsSuccessStatusCode, await retry.Content.ReadAsStringAsync());
        Assert.Equal(1, await f.Db.CustomerDebtAccounts.CountAsync());
        Assert.Equal(1, await f.Db.LedgerEntries.CountAsync());

        async Task AssertUnchanged()
        {
            f.Db.ChangeTracker.Clear();
            var saved = await f.Db.Invoices.AsNoTracking().SingleAsync();
            Assert.Equal(InvoiceStatus.Confirmed, saved.Status);
            Assert.Equal(invoice.RowVersion, saved.RowVersion);
            Assert.Null(saved.CustomerId);
            Assert.Empty(await f.Db.Customers.ToListAsync());
            Assert.Empty(await f.Db.CustomerDebtAccounts.ToListAsync());
            Assert.Empty(await f.Db.DebtItems.ToListAsync());
            Assert.Empty(await f.Db.LedgerEntries.ToListAsync());
            Assert.Empty(await f.Db.AccountTransactions.ToListAsync());
            Assert.Empty(await f.Db.InvoiceStatusHistory.ToListAsync());
            Assert.Empty(await f.Db.StockMovements.ToListAsync());
            var stock = await f.Db.Stocks.SingleAsync();
            Assert.Equal(20m, stock.Quantity);
            Assert.Equal(2m, stock.ReservedQuantity);
        }
    }

    [PostgresFact]
    public async Task TwoInvoicesReuseOneCustomerDebtAccountAndConcurrentReceiptsBothSucceed()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var customer = new Customer { Name = "Shared accounting customer", Phone = "+249123456789" };
        f.Db.Customers.Add(customer);
        await f.Db.SaveChangesAsync();
        var left = await Seed(f, actor.Id, InvoiceStatus.Confirmed, customer.Id);
        var right = await Seed(f, actor.Id, InvoiceStatus.Confirmed, customer.Id);
        var form = await client.GetStringAsync($"/Invoices/Fulfill/{left.Id}");
        var token = WorkspaceUiTests.Token(form);
        var shipped = await Task.WhenAll(Fulfill(client, left, token, Guid.NewGuid(), InvoiceStatus.Shipped),
            Fulfill(client, right, token, Guid.NewGuid(), InvoiceStatus.Delivered));
        foreach (var response in shipped) { using(response) Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); }
        Assert.Equal(1, await f.Db.CustomerDebtAccounts.CountAsync());
        Assert.Equal(2, await f.Db.DebtItems.CountAsync());
        f.Db.ChangeTracker.Clear();
        left = await f.Db.Invoices.SingleAsync(item => item.Id == left.Id);
        right = await f.Db.Invoices.SingleAsync(item => item.Id == right.Id);
        form = await client.GetStringAsync($"/Invoices/RecordPayment/{left.Id}");
        token = WorkspaceUiTests.Token(form);
        var receipts = await Task.WhenAll(Pay(client, left, token, Guid.NewGuid(), "3", "Cash"),
            Pay(client, right, token, Guid.NewGuid(), "3", "Cash"));
        foreach (var response in receipts) { using(response) Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); }
        Assert.Equal(14m, await Net(f.Db, AccountType.Receivables));
        Assert.Equal(6m, await Net(f.Db, AccountType.Treasury));
        Assert.Equal(-20m, await Net(f.Db, AccountType.Sales));
    }

    [PostgresFact]
    public async Task HistoricalUnpostedReceiptsRequireReconciliationBeforeRecognizingSales()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = await Seed(f, actor.Id, InvoiceStatus.Confirmed);
        invoice.PaidAmount = 3; invoice.RemainingAmount = 7; invoice.PaymentStatus = PaymentStatus.Partial;
        f.Db.Payments.Add(new Payment { InvoiceId = invoice.Id, Amount = 3, PaymentNumber = "LEGACY-UNPOSTED", PaymentType = PaymentType.Receipt });
        await f.Db.SaveChangesAsync();
        var form = await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}");
        using var rejected = await Fulfill(client, invoice, WorkspaceUiTests.Token(form), Guid.NewGuid(), InvoiceStatus.Shipped, "+249123456789");
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Empty(await f.Db.LedgerEntries.ToListAsync());
        Assert.Empty(await f.Db.DebtItems.ToListAsync());
        Assert.Empty(await f.Db.StockMovements.ToListAsync());
        Assert.Equal(1, await f.Db.Payments.CountAsync());
    }

    [Fact]
    public async Task FinancialPagesRequireTheExistingAdministratorPolicy()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        foreach (var route in new[] { "/FinancialAccounts", "/FinancialAccounts/Movements", "/FinancialAccounts/Treasury" })
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(route)).StatusCode);
        await WorkspaceUiTests.Login(client, "ui-reader");
        using var response = await client.GetAsync("/FinancialAccounts");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Denied", response.Headers.Location!.OriginalString);
    }

    private static async Task<Invoice> Seed(PostgresInventoryTests.Database f, Guid actor, InvoiceStatus status, Guid? customerId = null)
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), InvoiceNumber = "ACCOUNTING-" + Guid.NewGuid().ToString("N"),
            CustomerName = customerId.HasValue ? "Shared accounting customer" : "Accounting integration customer", CustomerId = customerId,
            UserId = actor, WarehouseId = f.Source, Status = status, SubTotal = 10, TotalAmount = 10, RemainingAmount = 10,
            Items = [new InvoiceItem { ProductId = f.Product, Quantity = 2, UnitPrice = 5, TotalAmount = 10 }] };
        var stock = await f.Db.Stocks.SingleOrDefaultAsync();
        if (stock is null) f.Db.Stocks.Add(new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 20,
            ReservedQuantity = status == InvoiceStatus.Confirmed ? 2 : 0 });
        else if (status == InvoiceStatus.Confirmed) stock.ReservedQuantity += 2;
        f.Db.Invoices.Add(invoice);
        await f.Db.SaveChangesAsync();
        return invoice;
    }

    private static Task<decimal> Net(NEXTDbContext db, AccountType type) => db.AccountTransactions
        .Where(line => line.AccountId == Account.IdFor(type)).SumAsync(line => line.Debit - line.Credit);

    private static Task<HttpResponseMessage> Pay(HttpClient client, Invoice invoice, string token, Guid operation, string amount,
        string method, string action = "RecordPayment") => WorkspaceUiTests.Post(client, $"/Invoices/{action}/{invoice.Id}", token,
            ("OperationId", operation.ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)), ("Amount", amount), ("PaymentMethod", method));

    private static Task<HttpResponseMessage> Fulfill(HttpClient client, Invoice invoice, string token, Guid operation,
        InvoiceStatus target, string? phone = null) => WorkspaceUiTests.Post(client, $"/Invoices/Fulfill/{invoice.Id}", token,
            ("OperationId", operation.ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("TargetStatus", target.ToString()), ("CustomerPhone", phone ?? ""));
}
