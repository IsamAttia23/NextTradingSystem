using Microsoft.Playwright;

namespace NEXT.web.Services;

/// <summary>One lazily started Chromium process; each export uses its own isolated context.</summary>
public sealed class InvoicePdfBrowser(IConfiguration configuration) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private IPlaywright? playwright;
    private IBrowser? browser;
    private bool disposed;

    public async Task<IBrowser> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (browser is { IsConnected: true })
                return browser;
            if (browser is not null)
                await browser.DisposeAsync();
            playwright?.Dispose();
            var path = configuration["InvoicePdf:BrowsersPath"] ??
                Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") ??
                Path.Combine(AppContext.BaseDirectory, "App_Data", "playwright");
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", path);
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Timeout = 15000 });
            ct.ThrowIfCancellationRequested();
            return browser;
        }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (browser is not null)
                    await browser.DisposeAsync();
            }
            finally { playwright?.Dispose(); }
        }
        finally { gate.Release(); }
    }
}
