using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Features.Accounting;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class AccountingLocalizationTests
{
    [Theory]
    [InlineData("ar", "سداد من قيمة فاتورة رقم INV-TEXT")]
    [InlineData("en", "Payment against invoice INV-TEXT")]
    public async Task KnownGeneratedAndLegacyDescriptionsFollowCultureAndFreeTextIsPreserved(string culture, string expected)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var text = scope.ServiceProvider.GetRequiredService<AccountingText>();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var item = new AccountTransactionDto(Guid.NewGuid(), Guid.NewGuid(), AccountType.Treasury, new(2026, 11, 22),
                AccountingDescriptions.InvoiceReceipt, 3, 0, null, Guid.NewGuid(), "INV-TEXT", Guid.NewGuid(), "Operator",
                PaymentMethod.Cash, "PAY-TEXT", "Customer", LedgerEntryKind.Receipt, PaymentPurpose.InvoiceReceipt);
            Assert.Equal(expected, text.Description(item));
            var legacy = item with { Description = "سداد من قيمة فاتورة رقم INV-TEXT" };
            Assert.Equal(expected, text.Description(legacy));
            Assert.Equal("سداد من قيمة فاتورة رقم INV-TEXT", legacy.Description);
            var custom = item with { Description = "ملاحظة خاصة بالعميل / custom note" };
            Assert.Equal(custom.Description, text.Description(custom));
            Assert.Contains(LedgerEntryKind.Receipt, text.Search("سداد").Kinds);
            Assert.Contains(LedgerEntryKind.Receipt, text.Search("PAYMENT").Kinds);
            Assert.Contains(PaymentMethod.BankTransfer, text.Search("BANK TRANSFER").Methods);
            Assert.Contains(PaymentMethod.BankTransfer, text.Search("تحويل").Methods);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [PostgresFact]
    public async Task PostgreSqlSearchFindsTranslatedOperationsMethodsAndCaseInsensitiveReferences()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var actor = await f.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = new Invoice { InvoiceNumber = "INV-LOCALIZED-SEARCH", UserId = actor.Id, WarehouseId = f.Source,
            CustomerName = "Mixed Case Customer", TotalAmount = 10, RemainingAmount = 10 };
        f.Db.Add(invoice); await f.Db.SaveChangesAsync();
        var form = await client.GetStringAsync($"/Invoices/RecordPayment/{invoice.Id}");
        using (var paid = await WorkspaceUiTests.Post(client, $"/Invoices/RecordPayment/{invoice.Id}", WorkspaceUiTests.Token(form),
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("Amount", "3"), ("PaymentMethod", "BankTransfer")))
            Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());
        Assert.Equal(AccountingDescriptions.InvoiceReceipt, (await f.Db.LedgerEntries.SingleAsync()).Description);
        foreach (var culture in new[] { "ar", "en" })
        foreach (var term in new[] { "PAYMENT", "سداد", "bank transfer", "تحويل", "mixed case", "inv-localized-search" })
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync("/FinancialAccounts/Movements?Account=Treasury&culture=" + culture + "&SearchTerm=" + Uri.EscapeDataString(term)));
            Assert.Contains(invoice.InvoiceNumber, html);
            Assert.Contains($"href=\"/Invoices/Details/{invoice.Id}\"", html);
            Assert.Contains(culture == "ar" ? "سداد من قيمة فاتورة رقم" : "Payment against invoice", html);
            Assert.DoesNotContain("AccountingDescriptionInvoiceReceipt", html);
        }
    }
}
