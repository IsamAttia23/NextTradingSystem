using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;
namespace NEXT.Tests;

public sealed class ManualInvoiceBrowserTests
{
    [PdfTheory]
    [InlineData("ar", 390)]
    [InlineData("en", 1280)]
    public async Task ManualPriceIsEditableAndCatalogPriceStaysLockedAcrossSaveAndEdit(string culture, int width)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var warehouse = new Warehouse { Name = "Manual browser warehouse", Code = "MANUAL", IsDefault = true };
            var product = new Product { Name = "Catalog browser item", CostPrice = 4 };
            db.Add(new Stock { Product = product, Warehouse = warehouse, Quantity = 10, AverageCost = 4, TotalCost = 40 });
            await db.SaveChangesAsync();
        }
        var browser = await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context = await browser.NewContextAsync(new() { Locale = culture, ViewportSize = new() { Width = width, Height = 950 } });
        var page = await context.NewPageAsync(); page.SetDefaultTimeout(20000);
        using var client = WorkspaceUiTests.Client(app); var origin = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin + "/Account/Login?culture=" + culture);
        await page.Locator("[name=UserName]").FillAsync("ui-admin");
        await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(origin + "/", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.GotoAsync(origin + "/Invoices?culture=" + culture);
        await page.Locator("[data-modal-url='/Invoices/Create']").ClickAsync();
        Task<string> Formatted(double value) => page.EvaluateAsync<string>("v => new Intl.NumberFormat(document.documentElement.lang, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v)", value);
        var modal = page.Locator("#workspaceModal.show");
        await modal.Locator("[data-invoice-form]").WaitForAsync();
        await modal.Locator("[name=CustomerName]").FillAsync("Manual browser customer");
        await modal.Locator("[data-add-manual-line]").ClickAsync();
        var manual = modal.Locator(".invoice-line[data-manual=true]");
        await manual.Locator("[data-manual-name]").FillAsync("Delivery fee / رسوم توصيل");
        await manual.Locator("[data-invoice-quantity]").FillAsync("2.5");
        await manual.Locator("[data-manual-price]").FillAsync("12.34");
        Assert.True(await manual.Locator("[data-manual-price]").IsEnabledAsync());
        Assert.Contains(await Formatted(30.85), await manual.Locator("[data-invoice-line-total]").InnerTextAsync());
        await modal.Locator("[data-add-invoice-line]").ClickAsync();
        var catalog = modal.Locator(".invoice-line[data-manual=false]");
        await catalog.Locator("[data-invoice-search]").FillAsync("Catalog browser");
        await catalog.Locator("[data-invoice-option]").ClickAsync();
        await catalog.Locator("[data-invoice-quantity]").FillAsync("1.5");
        Assert.False(await catalog.Locator("[data-manual-price]").IsEnabledAsync());
        Assert.Contains(await Formatted(4), await catalog.Locator("[data-invoice-price]").InnerTextAsync());
        Assert.Contains(await Formatted(36.85), await modal.Locator("[data-invoice-total]").InnerTextAsync());
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await Screenshot(page, culture + "-manual-invoice.png");
        await modal.Locator("[data-save-invoice]").ClickAsync();
        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/Invoices/Details/"));
        await page.Locator("[data-modal-url*='/Invoices/Edit/']").ClickAsync();
        await modal.Locator("[data-invoice-form]").WaitForAsync();
        Assert.Equal("12.34", await modal.Locator(".invoice-line[data-manual=true] [data-manual-price]").InputValueAsync());
        Assert.False(await modal.Locator(".invoice-line[data-manual=false] [data-manual-price]").IsEnabledAsync());
        await using var verifyScope = app.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var invoice = await verify.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync();
        Assert.Equal(36.85m, invoice.TotalAmount);
        Assert.Equal(2, invoice.Items.Count);
        Assert.Equal(1, await verify.Products.CountAsync());
        Assert.Empty(await verify.StockMovements.ToListAsync());
    }
    [PdfFact]
    public async Task ReviewedOpeningCanBeApprovedFromTheMobileAccountDetails()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            db.Add(new Stock { Product = new Product { Name = "Opening review item" },
                Warehouse = new Warehouse { Name = "Opening review warehouse", Code = "REVIEW" }, Quantity = 2, AverageCost = 5, TotalCost = 10 });
            await db.SaveChangesAsync();
        }
        var browser = await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context = await browser.NewContextAsync(new() { Locale = "ar", ViewportSize = new() { Width = 390, Height = 950 } });
        var page = await context.NewPageAsync(); page.SetDefaultTimeout(20000);
        using var client = WorkspaceUiTests.Client(app); var origin = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin + "/Account/Login?culture=ar");
        await page.Locator("[name=UserName]").FillAsync("ui-admin");
        await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(origin + "/", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.GotoAsync(origin + "/FinancialAccounts/Details?account=Inventory&culture=ar");
        await page.Locator("[data-modal-url*='ApproveOpening']").ClickAsync();
        var modal = page.Locator("#workspaceModal.show");
        await modal.Locator("[name=VerifiedBalance]").WaitForAsync();
        Assert.Equal("10", await modal.Locator("[name=VerifiedBalance]").InputValueAsync());
        Assert.True(await modal.Locator("[name=VerifiedBalance]").EvaluateAsync<bool>("input => input.readOnly"));
        await modal.Locator("[name=Reason]").FillAsync("Reviewed stock count / جرد معتمد");
        await modal.Locator("[type=submit]").ClickAsync();
        await page.Locator(".finance-action-card h2").Filter(new() { HasText = "الرصيد الافتتاحي المعتمد" }).WaitForAsync();
        Assert.Equal(0, await page.Locator("[data-modal-url*='ApproveOpening']").CountAsync());
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await Screenshot(page, "ar-approved-opening-mobile.png");
        await using var verifyScope = app.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        Assert.Equal(1, await verify.AccountOpenings.CountAsync());
        Assert.Equal(1, await verify.LedgerEntries.CountAsync());
        await page.GotoAsync(origin + "/FinancialAccounts/Reconciliation?culture=ar");
        Assert.Equal(0, await page.Locator("tbody .stock-low-tag").CountAsync());
    }

    private static async Task Screenshot(IPage page, string name)
    {
        var root = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        await page.ScreenshotAsync(new() { Path = Path.Combine(root, name), FullPage = true, Animations = ScreenshotAnimations.Disabled });
    }
}
