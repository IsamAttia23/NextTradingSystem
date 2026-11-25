using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceCancellationBrowserTests
{
    [PdfFact]
    public async Task PendingEditCancellationAndDeferredRefundWorkFromTheMobileInvoicePage()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        Guid id;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var actor = await db.Users.SingleAsync(item => item.UserName == "ui-admin");
            var product = new Product { Name = "Cancellation browser product", CostPrice = 4 };
            var warehouse = new Warehouse { Name = "Cancellation browser warehouse", Code = "CB", IsDefault = true };
            var invoice = new Invoice { InvoiceNumber = "CANCEL-BROWSER", UserId = actor.Id, CustomerName = "Browser customer",
                Warehouse = warehouse, SubTotal = 10, TotalAmount = 10, RemainingAmount = 10, Notes = "Original note",
                Items = [new InvoiceItem { Product = product, Quantity = 2, UnitPrice = 5, UnitCost = 4, TotalAmount = 10 }] };
            db.AddRange(invoice, new Stock { Product = product, Warehouse = warehouse, Quantity = 20 });
            await db.SaveChangesAsync(); id = invoice.Id;
        }
        var browser = await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context = await browser.NewContextAsync(new() { Locale = "ar", ViewportSize = new() { Width = 390, Height = 844 } });
        var page = await context.NewPageAsync(); page.SetDefaultTimeout(15000);
        using var client = WorkspaceUiTests.Client(app);
        var origin = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin + "/Account/Login?culture=ar");
        await page.Locator("[name=UserName]").FillAsync("ui-admin");
        await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(origin + "/");
        await page.GotoAsync(origin + $"/Invoices/Details/{id}?culture=ar");
        await page.Locator("[data-modal-url*='/Edit']").ClickAsync();
        var modal = page.Locator("#workspaceModal.show");
        await modal.Locator("[data-invoice-form]").WaitForAsync();
        Assert.Equal("Browser customer", await modal.Locator("[name=CustomerName]").InputValueAsync());
        Assert.Equal("2", await modal.Locator("[data-invoice-quantity]").InputValueAsync());
        Assert.Equal("Original note", await modal.Locator("[name=Notes]").InputValueAsync());
        await modal.Locator("[data-invoice-quantity]").FillAsync("3");
        await modal.Locator("[name=Notes]").FillAsync("Updated browser note");
        await Submit();
        Assert.Contains("Updated browser note", await page.Locator("#main").InnerTextAsync());
        await page.Locator("[data-modal-url*='/Confirm']").ClickAsync();
        await modal.Locator("[name=Amount]").FillAsync("2");
        await Submit();
        Assert.Equal(0, await page.Locator("[data-modal-url*='/Edit']").CountAsync());
        await page.Locator("[data-modal-url*='/Cancel']").ClickAsync();
        await modal.Locator("[name=Reason]").FillAsync("طلب العميل الإلغاء");
        Assert.Equal(0, await modal.Locator("[name=PaymentMethod]").CountAsync());
        await Submit();
        Assert.Equal(0, await page.Locator("[data-modal-url*='/RecordPayment']").CountAsync());
        Assert.Contains("ملغاة", await page.Locator(".page-heading p").InnerTextAsync());
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        var qa = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(qa))
        {
            Directory.CreateDirectory(qa);
            await page.ScreenshotAsync(new() { Path = Path.Combine(qa, "cancelled-invoice-credit-mobile.png"), FullPage = true });
        }
        await page.Locator("[data-modal-url*='/Refund']").ClickAsync();
        await modal.Locator("[name=PaymentMethod]").SelectOptionAsync("BankTransfer");
        await Submit();
        Assert.Equal(0, await page.Locator("[data-modal-url*='/Refund']").CountAsync());
        Assert.Contains("سجل رد المبالغ", await page.Locator("#main").InnerTextAsync());
        await using var check = app.Services.CreateAsyncScope();
        var finalDb = check.ServiceProvider.GetRequiredService<NEXTDbContext>();
        Assert.Equal(InvoiceStatus.Cancelled, (await finalDb.Invoices.SingleAsync()).Status);
        Assert.Equal(0m, (await finalDb.Stocks.SingleAsync()).ReservedQuantity);
        var refund = await finalDb.Payments.SingleAsync(item => item.Purpose == PaymentPurpose.CustomerRefund);
        Assert.Equal(2m, refund.Amount);
        Assert.Equal(PaymentMethod.BankTransfer, refund.PaymentMethod);

        async Task Submit()
        {
            await modal.Locator("button[type=submit]").ClickAsync();
            await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        }
    }
}
