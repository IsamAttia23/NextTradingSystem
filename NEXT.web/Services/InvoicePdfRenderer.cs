using Microsoft.Playwright;
using NEXT.Application.Exceptions;

namespace NEXT.web.Services;

public sealed class InvoicePdfRenderer(InvoicePdfBrowser browserHost, ILogger<InvoicePdfRenderer> logger)
{
    private readonly SemaphoreSlim slots = new(2);

    public async Task<byte[]> RenderAsync(string html, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var token = deadline.Token;
        var acquired = false;
        try
        {
            await slots.WaitAsync(token);
            acquired = true;
            var browser = await browserHost.GetAsync(token);
            await using var context = await browser.NewContextAsync(new()
            {
                JavaScriptEnabled = false, ServiceWorkers = ServiceWorkerPolicy.Block,
                AcceptDownloads = false
            }).WaitAsync(token);
            // Fonts and styles are embedded. User-supplied text cannot trigger network requests.
            await context.RouteAsync("**/*", route => route.AbortAsync()).WaitAsync(token);
            var page = await context.NewPageAsync().WaitAsync(token);
            await page.SetContentAsync(html, new() { Timeout = 15000 }).WaitAsync(token);
            await page.EvaluateAsync("async () => { await document.fonts.ready; }").WaitAsync(token);
            return await page.PdfAsync(new()
            {
                Format = "A4", PreferCSSPageSize = true, PrintBackground = true,
                DisplayHeaderFooter = true, HeaderTemplate = "<span></span>",
                FooterTemplate = "<div style='width:100%;text-align:center;font-size:9px;color:#69798e'>" +
                    "<span class='pageNumber'></span> / <span class='totalPages'></span></div>"
            }).WaitAsync(token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("InvoicePdfUnavailable");
        }
        catch (PlaywrightException error)
        {
            logger.LogError(error, "Invoice PDF generation failed");
            throw new DependencyUnavailableException("InvoicePdfUnavailable", error);
        }
        finally { if (acquired) slots.Release(); }
    }
}
