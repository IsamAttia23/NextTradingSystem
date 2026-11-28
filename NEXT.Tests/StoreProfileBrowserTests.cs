using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Application.Abstractions.Context;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class StoreProfileBrowserTests
{
    [PdfTheory]
    [InlineData("ar", 390)]
    [InlineData("ar", 1280)]
    [InlineData("en", 390)]
    [InlineData("en", 1280)]
    public async Task EditedStoreDetailsAppearOnEveryDocumentAndItsPdf(string culture, int width)
    {
        var actor = new AccountingFoundationTests.TestActor();
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: services => services.AddSingleton<ICurrentUserService>(actor));
        app.Configuration["Inventory:AllowInvoiceOverselling"] = "true";
        Guid invoiceId;
        Guid accountId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = await AccountingFoundationTests.Activate(scope.ServiceProvider, actor);
            var warehouse = new Warehouse { Id = Guid.NewGuid(), Name = "Document warehouse", Code = "DOC", IsDefault = true };
            var product = new Product { Id = Guid.NewGuid(), Name = "منتج تجريبي / QA item", CostPrice = 10 };
            db.AddRange(warehouse, product);
            await db.SaveChangesAsync();
            var customer = await DebtBookTests.Prepare(db, warehouse.Id, product.Id);
            invoiceId = await DebtBookTests.Sell(scope.ServiceProvider, db, customer, product.Id, 2, InvoiceStatus.Delivered);
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
        await page.GotoAsync(origin + "/StoreSettings?culture=" + culture);
        await page.Locator("[data-modal-url='/StoreSettings/Edit']").ClickAsync();
        var modal = page.Locator("#workspaceModal.show");
        const string store = "متجر الاختبار / QA Store";
        await modal.Locator("[name=Name]").FillAsync(store);
        await modal.Locator("[name=Subtitle]").FillAsync("تجارة الأجهزة / Appliance trading");
        await modal.Locator("[name=Address]").FillAsync("الخرطوم — السوق\nKhartoum — Market");
        await modal.Locator("[name=Phone]").FillAsync("+249123456789");
        await modal.Locator("[name=SecondaryPhone]").FillAsync("+249987654321");
        var saved = page.WaitForResponseAsync(r => r.Request.Method == "POST" && r.Url.Contains("/StoreSettings/Edit"));
        await modal.Locator("[type=submit]").ClickAsync();
        var response = await saved;
        Assert.True(response.Ok, await response.TextAsync());
        await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await page.Locator(".store-profile-card .document-business-name").WaitForAsync();
        Assert.Equal(store, await page.Locator(".store-profile-card .document-business-name").InnerTextAsync());
        await Shot(page, $"store-settings-{culture}-{width}.png");
        foreach (var doc in new[] {
            (Name: "invoice", Print: "/InvoiceDocuments/Print/" + invoiceId, Pdf: "/InvoiceDocuments/Pdf/" + invoiceId + "?download=true&culture=" + culture),
            (Name: "statement", Print: "/DebtBookDocuments/Print/" + accountId, Pdf: "/DebtBookDocuments/Pdf/" + accountId + "?culture=" + culture),
            (Name: "products", Print: "/ProductReports/Print", Pdf: "/ProductReports/Pdf?culture=" + culture) })
        {
            await page.GotoAsync(origin + doc.Print + "?culture=" + culture);
            Assert.Equal(store, await page.Locator(".document-business-name").InnerTextAsync());
            Assert.Contains("Appliance trading", await page.Locator(".document-business-subtitle").InnerTextAsync());
            Assert.Contains("Khartoum", await page.Locator(".document-business-address").InnerTextAsync());
            Assert.Contains("+249987654321", await page.Locator(".document-business-phones").InnerTextAsync());
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
            await Shot(page, $"store-{doc.Name}-{culture}-{width}.png");
            if (width == 1280)
            {
                var pdf = await context.APIRequest.GetAsync(origin + doc.Pdf);
                Assert.True(pdf.Ok, "PDF response status: " + pdf.Status);
                var bytes = await pdf.BodyAsync();
                Assert.True(bytes.Length > 1000);
                var qa = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
                if (!string.IsNullOrWhiteSpace(qa)) await File.WriteAllBytesAsync(Path.Combine(qa, $"store-{doc.Name}-{culture}.pdf"), bytes);
            }
        }
    }

    private static async Task Shot(IPage page, string file)
    {
        var qa = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
        if (string.IsNullOrWhiteSpace(qa)) return;
        Directory.CreateDirectory(qa);
        await page.ScreenshotAsync(new() { Path = Path.Combine(qa, file), FullPage = true, Animations = ScreenshotAnimations.Disabled, Style = ".notification-stack {visibility:hidden!important;}" });
    }
}
