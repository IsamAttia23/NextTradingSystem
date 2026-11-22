using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceAccountingBrowserTests
{
    [PdfTheory]
    [InlineData("ar")]
    [InlineData("en")]
    public async Task DispatchPhoneAndTreasuryInvoiceLinksWorkOnDesktopAndMobile(string culture)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        Guid id;
        const string number = "INVOICE-ACCOUNTING-UI";
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var actor = await db.Users.SingleAsync(item => item.UserName == "ui-admin");
            var warehouse = new Warehouse { Name = "Accounting browser warehouse", Code = "ACCOUNTING-UI", IsActive = true };
            var product = new Product { Name = "منتج الحسابات / Accounting product", IsActive = true };
            var invoice = new Invoice { InvoiceNumber = number, CustomerName = "عميل الحسابات / Accounting customer",
                UserId = actor.Id, Warehouse = warehouse, Status = InvoiceStatus.Confirmed, SubTotal = 10, TotalAmount = 10,
                RemainingAmount = 10, Items = [new InvoiceItem { Product = product, Quantity = 2, UnitPrice = 5, TotalAmount = 10 }] };
            db.AddRange(invoice, new Stock { Product = product, Warehouse = warehouse, Quantity = 10, ReservedQuantity = 2 });
            await db.SaveChangesAsync();
            id = invoice.Id;
        }
        var browser = await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context = await browser.NewContextAsync(new() { Locale = culture, ViewportSize = new() { Width = 1280, Height = 1000 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15000);
        using var client = WorkspaceUiTests.Client(app);
        var origin = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin + "/Account/Login?culture=" + culture);
        await page.Locator("[name=UserName]").FillAsync("ui-admin");
        await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(origin + "/");
        await page.GotoAsync(origin + $"/Invoices/Details/{id}?culture={culture}");
        await page.Locator("[data-modal-url*='/Fulfill']").ClickAsync();
        await page.Locator("#workspaceModal.show [name=CustomerPhone]").WaitForAsync();
        await page.Locator("#modalBody [name=CustomerPhone]").FillAsync("+249123456789");
        await page.Locator("#modalBody [type=submit]").ClickAsync();
        await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await page.Locator("[data-modal-url*='/RecordPayment']").ClickAsync();
        await page.Locator("#workspaceModal.show [name=Amount]").WaitForAsync();
        await page.Locator("#modalBody [name=Amount]").FillAsync("3");
        await page.Locator("#modalBody [type=submit]").ClickAsync();
        await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await page.GotoAsync(origin + "/FinancialAccounts/Movements?Account=Treasury&culture=" + culture);
        var link = page.Locator($"td a[href='/Invoices/Details/{id}']");
        Assert.Equal(1, await link.CountAsync());
        Assert.Equal(number, await link.InnerTextAsync());
        Assert.Contains(3m.ToString("N2", CultureInfo.GetCultureInfo(culture)), await page.Locator("tbody").InnerTextAsync());
        await Screenshot(page, culture + "-treasury-desktop.png");
        await page.SetViewportSizeAsync(390, 844);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await Screenshot(page, culture + "-treasury-mobile.png");
        await link.ClickAsync();
        await page.WaitForURLAsync(origin + $"/Invoices/Details/{id}");
        Assert.Equal(number, await page.Locator("#main h1").InnerTextAsync());
        await page.GotoAsync(origin + "/FinancialAccounts?culture=" + culture);
        Assert.Equal(7, await page.Locator("tbody tr").CountAsync());
        await Screenshot(page, culture + "-account-balances-mobile.png");
    }

    private static async Task Screenshot(IPage page, string name)
    {
        var root = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        await page.ScreenshotAsync(new() { Path = Path.Combine(root, name), FullPage = true, Animations = ScreenshotAnimations.Disabled });
    }
}
