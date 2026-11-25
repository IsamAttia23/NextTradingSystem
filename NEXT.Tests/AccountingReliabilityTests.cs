using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class AccountingReliabilityTests
{
    [Fact]
    public async Task BalancedSalesCannotInventRevenueOrAdvancesAndInvalidDatesAreRejected()
    {
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        await using var scope = app.Services.CreateAsyncScope();
        var db = await AccountingFoundationTests.Activate(scope.ServiceProvider, actor);
        var customer = new Customer { Name = "Reliability customer" };
        var invoice = new Invoice { InvoiceNumber = "FALSE-SALE", UserId = actor.UserId!.Value,
            Customer = customer, Warehouse = new Warehouse { Name = "Reliability warehouse", Code = "REL" },
            Status = InvoiceStatus.Shipped, TotalAmount = 100, PaidAmount = 100, RemainingAmount = 0 };
        db.Add(invoice); await db.SaveChangesAsync();
        var posting = scope.ServiceProvider.GetRequiredService<IAccountingPostingService>();
        var date = DateOnly.FromDateTime(DateTime.Now);
        PostingRequestDto Sale(decimal amount) => new(Guid.NewGuid(), LedgerEntryKind.SalesRecognition,
            AccountingReferenceType.Invoice, invoice.Id, date, "Incorrect sale",
            [new(AccountType.CustomerAdvances, amount, 0, customer.Id), new(AccountType.Sales, 0, amount, customer.Id)], invoice.Id);
        await Assert.ThrowsAsync<ConflictException>(() => posting.PostAsync(Sale(1), default));
        await Assert.ThrowsAsync<ConflictException>(() => posting.PostAsync(Sale(100), default)); // No posted customer advance exists.
        foreach (var invalid in new[] { default(DateOnly), date.AddDays(1) })
            await Assert.ThrowsAsync<AppValidationException>(() => posting.PostAsync(AccountingFoundationTests.TreasuryOpening(1) with { Date = invalid }, default));
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }

    [PostgresFact]
    public async Task PostedPaymentsAreOwnedImmutableAndCannotBeReversedWithoutCorrectingTheirSource()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection,
            configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        await using var scope = app.Services.CreateAsyncScope();
        var db = await AccountingFoundationTests.Activate(scope.ServiceProvider, actor);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var invoice = new Invoice { InvoiceNumber = "RELIABLE-INVOICE", UserId = actor.UserId!.Value,
            CustomerName = "Reliable customer", WarehouseId = f.Source, TotalAmount = 10, RemainingAmount = 10,
            Items = [new InvoiceItem { ProductId = f.Product, Quantity = 2, UnitPrice = 5, TotalAmount = 10 }] };
        f.Db.AddRange(invoice, new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 20 });
        await f.Db.SaveChangesAsync();
        var token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}"));
        using (var receipt = await Post(client, invoice, token, "RecordPayment", ("Amount", "3"), ("PaymentMethod", "Cash")))
            Assert.True(receipt.IsSuccessStatusCode, await receipt.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear();
        invoice = await f.Db.Invoices.SingleAsync();
        var payment = await f.Db.Payments.SingleAsync();
        Assert.NotNull(invoice.CustomerId);
        Assert.Equal(invoice.CustomerId, payment.CustomerId);
        Assert.All(await f.Db.AccountTransactions.ToListAsync(), line => Assert.Equal(invoice.CustomerId, line.CustomerId));
        var trackedPayment = await db.Payments.SingleAsync();
        trackedPayment.Amount = 4;
        await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        foreach (var sql in new[] { "UPDATE \"Payments\" SET \"Amount\" = 4", "DELETE FROM \"Payments\"" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => f.Db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal("23514", error.SqlState);
            Assert.Contains("AccountingPostedPaymentImmutable", error.MessageText);
        }
        var entry = await f.Db.LedgerEntries.SingleAsync();
        var reversals = scope.ServiceProvider.GetRequiredService<IAccountingReversalService>();
        await Assert.ThrowsAsync<ConflictException>(() => reversals.ReverseAsync(new(Guid.NewGuid(), entry.Id,
            DateOnly.FromDateTime(DateTime.Now), "Unsafe separate reversal"), default));
        var posting = scope.ServiceProvider.GetRequiredService<IAccountingPostingService>();
        await Assert.ThrowsAsync<ConflictException>(() => posting.PostAsync(new(Guid.NewGuid(), LedgerEntryKind.Reversal,
            AccountingReferenceType.Adjustment, entry.Id, DateOnly.FromDateTime(DateTime.Now), "Unsafe direct reversal",
            [new(AccountType.Treasury, 0, 3, invoice.CustomerId), new(AccountType.CustomerAdvances, 3, 0, invoice.CustomerId)],
            invoice.Id, payment.Id, entry.Id), default));
        await AssertFakeSqlSaleRejected(f, invoice, actor.UserId.Value);
        token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Confirm/{invoice.Id}"));
        using (var confirmed = await Post(client, invoice, token, "Confirm", ("Amount", "0")))
            Assert.True(confirmed.IsSuccessStatusCode, await confirmed.Content.ReadAsStringAsync());
        f.Db.ChangeTracker.Clear(); invoice = await f.Db.Invoices.SingleAsync();
        token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Invoices/Fulfill/{invoice.Id}"));
        using (var shipped = await Post(client, invoice, token, "Fulfill", ("TargetStatus", "Shipped"), ("CustomerPhone", "+249123456789")))
            Assert.True(shipped.IsSuccessStatusCode, await shipped.Content.ReadAsStringAsync());
        foreach (var sql in new[] { "UPDATE \"Invoices\" SET \"TotalAmount\" = 100", "UPDATE \"Invoices\" SET \"PaidAmount\" = 1, \"RemainingAmount\" = 9" })
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => f.Db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal("23514", error.SqlState);
        }
        f.Db.ChangeTracker.Clear(); invoice = await f.Db.Invoices.SingleAsync();
        Assert.Equal(10m, invoice.TotalAmount);
        Assert.Equal(3m, invoice.PaidAmount);
        Assert.Equal(7m, invoice.RemainingAmount);
    }

    [Fact]
    public async Task DispatchWithoutRevenueCannotSilentlyCollectAsAnAdvance()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var actor = await db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = new Invoice { InvoiceNumber = "UNPOSTED-DISPATCH", UserId = actor.Id, CustomerName = "Needs reconciliation",
            Warehouse = new Warehouse { Name = "Unposted warehouse", Code = "UNPOSTED" }, Status = InvoiceStatus.Shipped,
            TotalAmount = 10, RemainingAmount = 10 };
        db.Add(invoice); await db.SaveChangesAsync();
        var details = await client.GetStringAsync($"/Invoices/Details/{invoice.Id}");
        Assert.DoesNotContain("data-modal-url=\"/Invoices/RecordPayment", details);
        var token = WorkspaceUiTests.Token(await client.GetStringAsync("/Invoices/Create"));
        using var response = await Post(client, invoice, token, "RecordPayment", ("Amount", "1"), ("PaymentMethod", "Cash"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }

    private static async Task AssertFakeSqlSaleRejected(PostgresInventoryTests.Database f, Invoice invoice, Guid actor)
    {
        await using var db = f.NewContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var id = Guid.NewGuid(); var operation = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var cash = Account.IdFor(AccountType.Treasury); var sales = Account.IdFor(AccountType.Sales);
        var key = LedgerEntry.Key(LedgerEntryKind.SalesRecognition, AccountingReferenceType.Invoice, invoice.Id);
        var now = DateTimeOffset.UtcNow; var date = DateOnly.FromDateTime(now.DateTime);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "LedgerEntries" ("Id", "OperationId", "PostingKey", "RequestHash", "Kind", "ReferenceType", "ReferenceId", "InvoiceId", "EntryDate", "Description", "ActorId", "ActorName", "CreatedAt")
                VALUES ({id}, {operation}, {key}, {new string('A', 64)}, 1, 1, {invoice.Id}, {invoice.Id}, {date}, 'False sale', {actor}, 'Operator', {now});
            INSERT INTO "AccountTransactions" ("Id", "LedgerEntryId", "Sequence", "AccountId", "Debit", "Credit", "CustomerId")
                VALUES ({first}, {id}, 1, {cash}, 1, 0, {invoice.CustomerId}), ({second}, {id}, 2, {sales}, 0, 1, {invoice.CustomerId});
            UPDATE "LedgerEntries" SET "PostedAt" = {now} WHERE "Id" = {id};
            """);
        var error = await Assert.ThrowsAsync<PostgresException>(() => transaction.CommitAsync());
        Assert.Equal("23514", error.SqlState);
        Assert.Contains("AccountingInvoicePostingMismatch", error.MessageText);
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, Invoice invoice, string token, string action,
        params (string Key, string Value)[] fields) => WorkspaceUiTests.Post(client, $"/Invoices/{action}/{invoice.Id}", token,
            [("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)), .. fields]);
}
