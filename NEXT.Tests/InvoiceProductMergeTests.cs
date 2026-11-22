using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class InvoiceProductMergeTests
{
    [PostgresFact]
    public async Task CreationMergesBeforeRoundingAndRejectsStaleOrExcessDuplicateQuantities()
    {
        await using var f = await PostgresInventoryTests.Database.Create(migrate: true, withReceiptRate: true);
        (await f.Db.Warehouses.SingleAsync(item => item.Id == f.Source)).IsDefault = true;
        f.Db.Add(new Stock { ProductId = f.Product, WarehouseId = f.Source, Quantity = 10 });
        await f.Db.SaveChangesAsync();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var token = WorkspaceUiTests.Token(await client.GetStringAsync("/Invoices/Create"));
        var operation = Guid.NewGuid();
        using (var saved = await Post(operation, "0.001", "0.001"))
            Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
        using (var repeated = await Post(operation, "0.001", "0.001"))
            Assert.True(repeated.IsSuccessStatusCode, await repeated.Content.ReadAsStringAsync());
        var invoice = await f.Db.Invoices.AsNoTracking().Include(item => item.Items).SingleAsync();
        var line = Assert.Single(invoice.Items);
        Assert.Equal(f.Product, line.ProductId);
        Assert.Equal(0.002m, line.Quantity);
        Assert.Equal(4m, line.UnitPrice);
        Assert.Equal(0.01m, line.TotalAmount);
        Assert.Equal(0.01m, invoice.SubTotal);
        Assert.Equal(0.01m, invoice.RemainingAmount);
        using (var stale = await Post(Guid.NewGuid(), "1", "1", "5"))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var shortage = await Post(Guid.NewGuid(), "6", "6"))
            Assert.Equal(HttpStatusCode.BadRequest, shortage.StatusCode);
        using (var excessive = await Post(Guid.NewGuid(), "999999999999.999", "0.001"))
            Assert.Equal(HttpStatusCode.BadRequest, excessive.StatusCode);
        Assert.Equal(1, await f.Db.Invoices.CountAsync());
        Assert.Equal(1, await f.Db.InvoiceItems.CountAsync());
        Assert.Equal(10m, (await f.Db.Stocks.SingleAsync()).Quantity);

        Task<HttpResponseMessage> Post(Guid id, string first, string second, string secondPrice = "4") =>
            WorkspaceUiTests.Post(client, "/Invoices/Create", token,
                ("OperationId", id.ToString()), ("CustomerName", "Merge customer"),
                ("Lines[0].ProductId", f.Product.ToString()), ("Lines[0].Quantity", first), ("Lines[0].QuotedUnitPrice", "4"),
                ("Lines[1].ProductId", f.Product.ToString()), ("Lines[1].Quantity", second), ("Lines[1].QuotedUnitPrice", secondPrice));
    }

    [PdfFact]
    public async Task ProductPickerMergesDuplicateLinesAndPreservesTotalsAndStockValidation()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var product = new Product { Name = "Merge product", CostPrice = 4 };
            var warehouse = new Warehouse { Name = "Merge warehouse", Code = "MERGE", IsDefault = true };
            db.AddRange(product, warehouse);
            db.Add(new Stock { Product = product, Warehouse = warehouse, Quantity = 10 });
            await db.SaveChangesAsync();
        }
        var browser = await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15000);
        using var client = WorkspaceUiTests.Client(app);
        var origin = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin + "/Account/Login?culture=en");
        await page.Locator("[name=UserName]").FillAsync("ui-admin");
        await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(origin + "/");
        await page.GotoAsync(origin + "/Invoices?culture=en");
        await page.Locator("[data-modal-url='/Invoices/Create']").ClickAsync();
        var form = page.Locator("#modalBody [data-invoice-form]");
        await form.WaitForAsync();
        await page.WaitForTimeoutAsync(400);
        await form.Locator("[name=CustomerName]").FillAsync("Merge customer");
        await ChooseProduct();
        await form.Locator("[data-invoice-quantity]").FillAsync("2");
        await form.Locator("[data-add-invoice-line]").ClickAsync();
        await form.Locator(".invoice-line").Last.Locator("[data-invoice-quantity]").FillAsync("3");
        await ChooseProduct();
        Assert.Equal(1, await form.Locator(".invoice-line").CountAsync());
        Assert.Equal("5", await form.Locator("[data-invoice-quantity]").InputValueAsync());
        Assert.Contains("20.00", await form.Locator("[data-invoice-total]").InnerTextAsync());
        Assert.True(await form.Locator("[data-save-invoice]").IsEnabledAsync());
        await form.Locator("[data-add-invoice-line]").ClickAsync();
        await ChooseProduct();
        Assert.Equal(1, await form.Locator(".invoice-line").CountAsync());
        Assert.Equal("6", await form.Locator("[name='Lines[0].Quantity']").InputValueAsync());
        Assert.Equal(1, await form.Locator("[name='Lines[0].ProductId']").CountAsync());
        Assert.Equal(0, await form.Locator("[name='Lines[1].ProductId']").CountAsync());
        await form.Locator("[data-invoice-quantity]").FillAsync("11");
        Assert.True(await form.Locator("[data-save-invoice]").IsDisabledAsync());
        await form.Locator("[data-invoice-quantity]").FillAsync("10");
        Assert.True(await form.Locator("[data-save-invoice]").IsEnabledAsync());
        Assert.Contains("40.00", await form.Locator("[data-invoice-total]").InnerTextAsync());
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));

        async Task ChooseProduct()
        {
            var picker = form.Locator(".invoice-line").Last.Locator("[data-invoice-picker=product]");
            await picker.Locator("[data-invoice-search]").FillAsync("Merge");
            await picker.Locator("[data-invoice-option]").First.ClickAsync();
        }
    }
}
