using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class PdfFactAttribute : FactAttribute
{
    public PdfFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXT_RUN_PDF_TESTS") != "1")
            Skip = "Install the PDF browser and set NEXT_RUN_PDF_TESTS=1.";
    }
}

public sealed class PdfTheoryAttribute : TheoryAttribute
{
    public PdfTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXT_RUN_PDF_TESTS") != "1")
            Skip = "Install the PDF browser and set NEXT_RUN_PDF_TESTS=1.";
    }
}

public sealed class InvoiceDocumentTests
{
    [PdfTheory]
    [InlineData("ar")]
    [InlineData("en")]
    public async Task DocumentColumnsAlignInPreviewAndPrintAtDesktopAndMobileWidths(string culture)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        var invoice = await Seed(app.Services);
        var browser = await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context = await browser.NewContextAsync(new() { Locale = culture, ViewportSize = new() { Width = 1280, Height = 1000 } });
        var page = await context.NewPageAsync();
        using var client = WorkspaceUiTests.Client(app);
        var origin = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin + "/Account/Login?culture=" + culture);
        await page.Locator("[name=UserName]").FillAsync("ui-admin");
        await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(origin + "/");
        await page.GotoAsync(origin + $"/Invoices/Details/{invoice.Id}?culture={culture}");
        await page.Locator("[data-modal-url*='InvoiceDocuments/Preview']").ClickAsync();
        await page.Locator("#workspaceModal.show [data-invoice-preview]").WaitForAsync();
        await page.WaitForFunctionAsync("getComputedStyle(document.querySelector('#workspaceModal')).opacity === '1'");
        await page.EvaluateAsync("document.fonts.ready");
        Assert.Equal(culture, await page.Locator(".invoice-document").GetAttributeAsync("lang"));
        await AssertColumns(page);
        await Screenshot(page, culture + "-invoice-alignment-desktop.png");
        await page.SetViewportSizeAsync(390, 844);
        await AssertColumns(page);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await Screenshot(page, culture + "-invoice-alignment-mobile.png");
        await page.SetViewportSizeAsync(1280, 1000);
        await page.GotoAsync(origin + $"/InvoiceDocuments/Print/{invoice.Id}?culture={culture}");
        await page.EmulateMediaAsync(new() { Media = Media.Print });
        await AssertColumns(page);
    }

    private static async Task AssertColumns(IPage page)
    {
        Assert.True(await page.Locator(".invoice-document-lines").EvaluateAsync<bool>("""
            table => {
                const headings = [...table.querySelectorAll('thead th')];
                return [...table.querySelectorAll('tbody tr')].every(row => [...row.cells].every((cell, index) => {
                    const heading = headings[index];
                    const a = heading.getBoundingClientRect(), b = cell.getBoundingClientRect();
                    const x = getComputedStyle(heading), y = getComputedStyle(cell);
                    return Math.abs(a.left - b.left) < 1 && Math.abs(a.right - b.right) < 1 &&
                        x.textAlign === y.textAlign && x.paddingLeft === y.paddingLeft && x.paddingRight === y.paddingRight;
                }));
            }
            """));
    }

    [Fact]
    public async Task DocumentEndpointsKeepAuthorizationAndMissingInvoiceChecks()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        var routes = new[] { "Preview", "Print", "Pdf" };
        foreach (var action in routes)
        {
            using var anonymous = await client.GetAsync($"/InvoiceDocuments/{action}/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        }
        using var reader = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(reader, "ui-reader");
        foreach (var action in routes)
        {
            using var denied = await reader.GetAsync($"/InvoiceDocuments/{action}/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("Denied", denied.Headers.Location!.OriginalString);
        }
        await WorkspaceUiTests.Login(client, "ui-admin");
        foreach (var action in routes)
        {
            using var missing = await client.GetAsync($"/InvoiceDocuments/{action}/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
    }

    [PdfTheory]
    [InlineData("ar")]
    [InlineData("en")]
    public async Task PreviewPrintAndPdfShareLocalizedDataWithoutChangingTheInvoice(string culture)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        var invoice = await Seed(app.Services);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var preview = await client.GetStringAsync($"/InvoiceDocuments/Preview/{invoice.Id}?culture={culture}");
        Assert.Contains("data-modal-view", preview);
        Assert.DoesNotContain("data-modal-form", preview);
        Assert.Contains("data-invoice-preview", preview);
        var format = System.Globalization.CultureInfo.GetCultureInfo(culture);
        Assert.Contains(10000m.ToString("N2", format) + " SDG", WebUtility.HtmlDecode(preview));
        Assert.Contains(7000m.ToString("N2", format) + " SDG", WebUtility.HtmlDecode(preview));
        Assert.Contains("download=True", preview, StringComparison.OrdinalIgnoreCase);
        var print = await client.GetStringAsync($"/InvoiceDocuments/Print/{invoice.Id}?culture={culture}");
        Assert.Contains(culture == "ar" ? "dir=\"rtl\"" : "dir=\"ltr\"", print);
        Assert.Contains(culture == "ar" ? "فاتورة مبيعات" : "Sales invoice", WebUtility.HtmlDecode(print));
        Assert.Contains("data-print-invoice", print);
        Assert.DoesNotContain("app-shell", print);
        Assert.Contains("data:font/ttf;base64,", print);
        var pdfRoute = $"/InvoiceDocuments/Pdf/{invoice.Id}?culture={culture}";
        using var inline = await client.GetAsync(pdfRoute);
        Assert.True(inline.IsSuccessStatusCode, await inline.Content.ReadAsStringAsync());
        Assert.Equal("application/pdf", inline.Content.Headers.ContentType!.MediaType);
        Assert.Equal("inline", inline.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(invoice.InvoiceNumber + ".pdf", inline.Content.Headers.ContentDisposition.FileNameStar);
        var bytes = await inline.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 5000);
        await Save(culture + "-invoice.pdf", bytes);
        using var download = await client.GetAsync(pdfRoute + "&download=true");
        Assert.True(download.IsSuccessStatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(invoice.InvoiceNumber + ".pdf", download.Content.Headers.ContentDisposition.FileNameStar);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var after = await db.Invoices.AsNoTracking().SingleAsync(item => item.Id == invoice.Id);
        Assert.Equal(invoice.RowVersion, after.RowVersion);
        Assert.Equal(3000m, after.PaidAmount);
        Assert.Equal(7000m, after.RemainingAmount);
        Assert.Equal(PaymentStatus.Partial, after.PaymentStatus);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.Stocks.ToListAsync());
    }

    [PdfFact]
    public async Task LongPendingInvoiceExportsEveryLineAndEncodesUserText()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        var invoice = await Seed(app.Services, longInvoice: true);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var preview = await client.GetStringAsync($"/InvoiceDocuments/Preview/{invoice.Id}?culture=ar");
        Assert.DoesNotContain("<script>alert", preview);
        Assert.Contains("&lt;script&gt;", preview);
        Assert.Contains("معلقة", WebUtility.HtmlDecode(preview));
        Assert.Contains("LINE-080", preview);
        using var result = await client.GetAsync($"/InvoiceDocuments/Pdf/{invoice.Id}?culture=ar");
        Assert.True(result.IsSuccessStatusCode, await result.Content.ReadAsStringAsync());
        await Save("ar-long-invoice.pdf", await result.Content.ReadAsByteArrayAsync());
        using var english = await client.GetAsync($"/InvoiceDocuments/Pdf/{invoice.Id}?culture=en");
        Assert.True(english.IsSuccessStatusCode, await english.Content.ReadAsStringAsync());
        await Save("en-long-invoice.pdf", await english.Content.ReadAsByteArrayAsync());
    }

    [PdfFact]
    public async Task SharedModalSupportsReadonlyPreviewMobileAndExistingDiscardConfirmation()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        var invoice = await Seed(app.Services);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var row = await db.Invoices.SingleAsync(item => item.Id == invoice.Id);
            row.Status = InvoiceStatus.Pending; row.PaidAmount = 0; row.RemainingAmount = row.TotalAmount;
            row.PaymentStatus = PaymentStatus.Unpaid;
            await db.SaveChangesAsync();
        }
        var browser = await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context = await browser.NewContextAsync(new() { AcceptDownloads = true, ViewportSize = new() { Width = 1280, Height = 1000 } });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15000);
        using var client = WorkspaceUiTests.Client(app);
        var origin = client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin + "/Account/Login?culture=ar");
        await page.Locator("[name=UserName]").FillAsync("ui-admin");
        await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(origin + "/");
        await page.GotoAsync(origin + $"/Invoices/Details/{invoice.Id}?culture=ar");
        await page.Locator("[data-modal-url*='InvoiceDocuments/Preview']").ClickAsync();
        await page.Locator("#workspaceModal.show [data-invoice-preview]").WaitForAsync();
        Assert.Equal(3, await page.Locator("[data-invoice-preview] .invoice-document-actions a").CountAsync());
        Assert.Contains("blur", await page.Locator(".modal-backdrop").EvaluateAsync<string>("el => getComputedStyle(el).backdropFilter"));
        await Screenshot(page, "invoice-modal-desktop.png");
        var detailsUrl = page.Url;
        var download = await page.RunAndWaitForDownloadAsync(() =>
            page.Locator("[data-invoice-preview] a[href*='download=']").ClickAsync(), new() { Timeout = 8000 });
        Assert.Equal(invoice.InvoiceNumber + ".pdf", download.SuggestedFilename);
        Assert.Null(await download.FailureAsync());
        var saved = await File.ReadAllBytesAsync((await download.PathAsync())!);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(saved, 0, 5));
        Assert.Equal(detailsUrl, page.Url);
        Assert.True(await page.Locator("#workspaceModal.show [data-invoice-preview]").IsVisibleAsync());
        await page.SetViewportSizeAsync(390, 844);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await Screenshot(page, "invoice-modal-mobile.png");
        await page.Locator("#modalBody [data-bs-dismiss=modal]").ClickAsync();
        await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await page.Locator("[data-modal-url*='/Confirm']").ClickAsync();
        await page.Locator("#workspaceModal.show [data-invoice-confirmation]").WaitForAsync();
        await page.Locator("#modalBody [name=Amount]").FillAsync("1");
        await page.Locator("#modalBody [data-bs-dismiss=modal]").ClickAsync();
        await page.Locator("#modalDiscard").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await page.Locator("[data-discard-changes]").ClickAsync();
        await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await page.AddInitScriptAsync("window.print = () => { window.invoicePrintCalls = (window.invoicePrintCalls || 0) + 1; };");
        await page.GotoAsync(origin + $"/InvoiceDocuments/Print/{invoice.Id}?autoPrint=true&culture=ar");
        await page.WaitForFunctionAsync("window.invoicePrintCalls === 1");
        await page.Locator("[data-print-invoice]").ClickAsync();
        await page.WaitForFunctionAsync("window.invoicePrintCalls === 2");
        var printDownload = await page.RunAndWaitForDownloadAsync(() =>
            page.Locator(".invoice-document-actions a[href*='download=']").ClickAsync());
        Assert.Equal(invoice.InvoiceNumber + ".pdf", printDownload.SuggestedFilename);
        Assert.Null(await printDownload.FailureAsync());
    }

    private static async Task<Invoice> Seed(IServiceProvider services, bool longInvoice = false)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var user = await db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var warehouse = new Warehouse { Name = "Document warehouse", Code = "DOC" };
        var headset = new Product { Name = "سماعات لاسلكية / Wireless headset" };
        var keyboard = new Product { Name = longInvoice
            ? "لوحة مفاتيح احترافية متعددة اللغات باسم طويل للاختبار / Professional multilingual keyboard with a long product name"
            : "لوحة مفاتيح / Keyboard" };
        var date = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var lines = longInvoice ? Enumerable.Range(1, 80).Select(index => new InvoiceItem
        {
            Product = new Product { Name = "LINE-" + index.ToString("D3") + " · " + keyboard.Name },
            Quantity = 2, UnitPrice = 3000, TotalAmount = 6000, CreatedAt = date.AddSeconds(index)
        }).ToList() : new List<InvoiceItem>
        {
            new() { Product = headset, Quantity = 2, UnitPrice = 2500, TotalAmount = 5000, CreatedAt = date },
            new() { Product = keyboard, Quantity = 2, UnitPrice = 3000, TotalAmount = 6000, CreatedAt = date.AddSeconds(1) }
        };
        var subtotal = lines.Sum(item => item.TotalAmount);
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-11202026-01", CustomerName = longInvoice ? "أحمد محمد <script>alert('x')</script>" : "أحمد محمد",
            CreatedByFullName = "محمد علي", UserId = user.Id, Warehouse = warehouse, InvoiceDate = date,
            SubTotal = subtotal, DiscountAmount = 1000, TotalAmount = subtotal - 1000,
            PaidAmount = longInvoice ? 0 : 3000, RemainingAmount = subtotal - 1000 - (longInvoice ? 0 : 3000),
            PaymentStatus = longInvoice ? PaymentStatus.Unpaid : PaymentStatus.Partial,
            Status = longInvoice ? InvoiceStatus.Pending : InvoiceStatus.Confirmed, Items = lines,
            Notes = "ملاحظة الفاتورة / Invoice note\nSecond line <script>alert('note')</script>"
        };
        db.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    private static async Task Save(string name, byte[] bytes)
    {
        var root = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(Path.Combine(root, name), bytes);
    }

    private static async Task Screenshot(IPage page, string name)
    {
        var root = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        await page.ScreenshotAsync(new() { Path = Path.Combine(root, name), FullPage = true });
    }
}
