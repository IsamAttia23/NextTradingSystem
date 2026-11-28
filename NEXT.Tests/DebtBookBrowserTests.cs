using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Application.Abstractions.Context;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;
namespace NEXT.Tests;

public sealed class DebtBookBrowserTests
{
    [PdfTheory]
    [InlineData("ar", 390)]
    [InlineData("ar", 1280)]
    [InlineData("en", 390)]
    [InlineData("en", 1280)]
    public async Task CustomerBookReceivesOnePaymentWithdrawsGoodsAndExportsAllItems(string culture, int width)
    {
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: s => s.AddSingleton<ICurrentUserService>(actor));
        app.Configuration["Inventory:AllowInvoiceOverselling"] = "true";
        Guid accountId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var db = await AccountingFoundationTests.Activate(sp, actor);
            var w = new Warehouse { Id = Guid.NewGuid(), Name = "Book warehouse", Code = "BOOK", IsDefault = true };
            var p = new Product { Id = Guid.NewGuid(), Name = "منتج حساب محمد / Mohammed item", CostPrice = 10 };
            db.AddRange(w, p);
            await db.SaveChangesAsync();
            var customer = await DebtBookTests.Prepare(db, w.Id, p.Id);
            await DebtBookTests.Sell(sp, db, customer, p.Id, 2, InvoiceStatus.Shipped);
            await DebtBookTests.Sell(sp, db, customer, p.Id, 3, InvoiceStatus.Delivered);
            accountId = (await db.CustomerDebtAccounts.SingleAsync()).Id;
        }
        var browser = await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context = await browser.NewContextAsync(new() { Locale = culture, ViewportSize = new() { Width = width, Height = 1000 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(20000);
        using var client = WorkspaceUiTests.Client(app);
        var origin = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin + "/Account/Login?culture=" + culture);
        await page.Locator("[name=UserName]").FillAsync("ui-admin");
        await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) [type=submit]").ClickAsync();
        await page.WaitForURLAsync(origin + "/");
        await page.GotoAsync(origin + "/DebtBook?culture=" + culture);
        Assert.Equal(1, await page.Locator("tbody tr").CountAsync());
        Assert.Contains("Mohammed", await page.Locator("tbody").InnerTextAsync());
        await Shot(page, $"debt-book-list-{culture}-{width}.png");
        await page.Locator($"a[href='/DebtBook/Details/{accountId}']").ClickAsync();
        await page.Locator("[data-modal-url*='/Receive/']").WaitForAsync();
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth"));
        await page.Locator("[data-modal-url*='/Receive/']").ClickAsync();
        var modal = page.Locator("#workspaceModal.show");
        await modal.Locator("[name=Amount]").FillAsync("25");
        await modal.Locator("[name=PaymentMethod]").SelectOptionAsync("Wallet");
        await modal.Locator("[name=CashAccount]").SelectOptionAsync("Wallet");
        Assert.True(await modal.Locator("[name=Notes]").EvaluateAsync<bool>("e=>e.required"));
        await modal.Locator("[name=Notes]").FillAsync("دفعة حساب / account payment");
        var paidResponse = page.WaitForResponseAsync(r => r.Request.Method == "POST" && r.Url.Contains("/DebtBook/Receive/"));
        await modal.Locator("[type=submit]").ClickAsync();
        var paid = await paidResponse;
        Assert.True(paid.Ok, await paid.TextAsync());
        await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Assert.Contains(25m.ToString("N2", CultureInfo.GetCultureInfo(culture)), await page.Locator(".finance-summary").InnerTextAsync());
        await page.Locator("[data-modal-url*='/Withdraw/']").ClickAsync();
        await modal.Locator("[data-invoice-form]").WaitForAsync();
        Assert.True(await modal.Locator("[name=CustomerName]").EvaluateAsync<bool>("e=>e.readOnly"));
        await modal.Locator("[data-add-manual-line]").ClickAsync();
        await modal.Locator("[data-manual-name]").FillAsync("خدمة توصيل / Delivery service");
        await modal.Locator("[data-invoice-quantity]").FillAsync("2");
        await modal.Locator("[data-manual-price]").FillAsync("3");
        await modal.Locator("[data-add-invoice-line]").ClickAsync();
        var catalog = modal.Locator(".invoice-line[data-manual=false]");
        await catalog.Locator("[data-invoice-search]").FillAsync("Mohammed item");
        await catalog.Locator("[data-invoice-option]").ClickAsync();
        await catalog.Locator("[data-invoice-quantity]").FillAsync("1");
        Assert.True(await modal.Locator("[data-save-invoice]").IsEnabledAsync());
        var withdrawnResponse = page.WaitForResponseAsync(r => r.Request.Method == "POST" && r.Url.Contains("/DebtBook/Withdraw/"));
        await modal.Locator("[data-save-invoice]").ClickAsync();
        var withdrawn = await withdrawnResponse;
        Assert.True(withdrawn.Ok, await withdrawn.TextAsync());
        await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        Assert.Contains(41m.ToString("N2", CultureInfo.GetCultureInfo(culture)), await page.Locator(".finance-summary").InnerTextAsync());
        Assert.Equal(3, await page.Locator(".financial-debt-invoices tbody tr").CountAsync());
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth"));
        Assert.True(await page.Locator(".table-responsive").EvaluateAllAsync<bool>("els=>els.every(e=>e.scrollWidth<=e.clientWidth+1)"));
        await Shot(page, $"debt-book-account-{culture}-{width}.png");
        await page.Locator("[data-modal-url*='DebtBookDocuments/Preview']").ClickAsync();
        await modal.Locator(".debt-statement").WaitForAsync();
        Assert.Equal(3, await modal.Locator(".debt-statement-goods").CountAsync());
        Assert.Equal(1, await modal.Locator(".debt-statement-payments tbody tr").CountAsync());
        Assert.Contains("Delivery service", await modal.InnerTextAsync());
        Assert.Contains("دفعة حساب / account payment", await modal.InnerTextAsync());
        var expectedTitle = (culture == "ar" ? "كشف حساب" : "Account statement") + " — محمد / Mohammed";
        Assert.Equal(expectedTitle, await modal.Locator(".debt-statement h1").InnerTextAsync());
        var printPage = await context.NewPageAsync();
        await printPage.GotoAsync(origin + "/DebtBookDocuments/Print/" + accountId + "?culture=" + culture);
        Assert.Equal(expectedTitle, await printPage.TitleAsync());
        Assert.Equal(expectedTitle, await printPage.Locator(".debt-statement h1").InnerTextAsync());
        await printPage.CloseAsync();
        await Shot(page, $"debt-book-statement-{culture}-{width}.png");
        var download = await page.RunAndWaitForDownloadAsync(() => modal.Locator("a[href*='/DebtBookDocuments/Pdf/']").ClickAsync());
        Assert.Equal(expectedTitle.Replace('/', '_') + ".pdf", download.SuggestedFilename);
        var path = await download.PathAsync();
        Assert.NotNull(path);
        Assert.True(new FileInfo(path!).Length > 1000);
        var qa = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(qa))
            await download.SaveAsAsync(Path.Combine(qa, $"debt-statement-{culture}-{width}.pdf"));
        await using var verifyScope = app.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        Assert.Equal(1, await verify.DebtAccountPayments.CountAsync());
        Assert.Equal(3, await verify.Invoices.CountAsync());
        Assert.Equal(-6, (await verify.Stocks.SingleAsync()).Quantity);
    }
    private static async Task Shot(IPage p, string name) { var qa = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY"); if (string.IsNullOrWhiteSpace(qa)) return; Directory.CreateDirectory(qa); await p.ScreenshotAsync(new() { Path = Path.Combine(qa, name), FullPage = true, Animations = ScreenshotAnimations.Disabled, Style = ".notification-stack { visibility: hidden !important; }" }); }
}
