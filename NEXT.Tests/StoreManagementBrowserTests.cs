using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Infrastructure.Stores;
using Npgsql;
using Xunit;

namespace NEXT.Tests;

public sealed class StoreBrowserTheoryAttribute : TheoryAttribute
{
    public StoreBrowserTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXT_RUN_POSTGRES_TESTS") != "1" ||
            Environment.GetEnvironmentVariable("NEXT_RUN_PDF_TESTS") != "1")
            Skip = "Set NEXT_RUN_POSTGRES_TESTS=1 and NEXT_RUN_PDF_TESTS=1 with local PostgreSQL and the PDF browser installed.";
    }
}

public sealed class StoreManagementBrowserTests
{
    [StoreBrowserTheory]
    [InlineData("ar", 390)]
    [InlineData("en", 1280)]
    public async Task ManagerCreatesAStoreAndItsOwnerSignsIntoTheCorrectWorkspace(string culture, int width)
    {
        await using var fixture = await PostgresInventoryTests.Database.Create(migrate: true);
        var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection,
            configureConfiguration: config =>
            {
                config["Stores:Enabled"] = "true";
                config["Email:Smtp:Host"] = "";
                config["Email:Resend:ApiKey"] = "";
                config["Email:Gmail:ClientId"] = "";
            });
        Guid operation = Guid.Empty;
        try
        {
            var browser = await app.Services.GetRequiredService<NEXT.web.Services.InvoicePdfBrowser>().GetAsync(default);
            await using var context = await browser.NewContextAsync(new() { Locale = culture, ViewportSize = new() { Width = width, Height = 1000 } });
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(30000);
            using var client = WorkspaceUiTests.Client(app);
            var origin = client.BaseAddress!.ToString().TrimEnd('/');
            await page.GotoAsync(origin + "/Account/Login?culture=" + culture);
            await page.Locator("[name=UserName]").FillAsync("ui-admin");
            await page.Locator("[name=Password]").FillAsync("Test-only!2026");
            await page.Locator("form:has([name=UserName]) [type=submit]").ClickAsync();
            await page.WaitForURLAsync(origin + "/");
            await page.GotoAsync(origin + "/Stores?culture=" + culture);
            await page.Locator("[data-modal-url='/Stores/Create']").ClickAsync();
            var modal = page.Locator("#workspaceModal.show");
            await modal.Locator("[name=OperationId]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
            operation = Guid.Parse(await modal.Locator("[name=OperationId]").InputValueAsync());
            const string name = "محل التجربة / Trial store";
            await modal.Locator("[name=Name]").FillAsync(name);
            await modal.Locator("[name=OwnerName]").FillAsync("مالك محل التجربة / Trial owner");
            await modal.Locator("[name=OwnerUserName]").FillAsync("trial-owner");
            await modal.Locator("[name=OwnerEmail]").FillAsync("trial-owner@example.test");
            await modal.Locator("[name=OwnerPassword]").FillAsync("Trial-Owner!Strong-2026");
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth"));
            var createdResponse = page.WaitForResponseAsync(r => r.Request.Method == "POST" && r.Url.EndsWith("/Stores/Create"), new() { Timeout = 120000 });
            await modal.Locator("[type=submit]").ClickAsync();
            var created = await createdResponse;
            Assert.True(created.Ok, await created.TextAsync());
            var result = await created.JsonAsync();
            var storeUrl = result!.Value.GetProperty("storeUrl").GetString()!;
            await page.Locator("#workspaceModal").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            await page.Locator(".store-entry-link").WaitForAsync();
            await Shot(page, $"stores-{culture}-{width}.png");
            // The browser still has the main administrator cookie, which must not sign into the new store.
            await page.GotoAsync(origin + storeUrl);
            Assert.Contains(storeUrl + "Account/Login", page.Url);
            Assert.Equal(name, await page.Locator(".store-login-name").InnerTextAsync());
            await Shot(page, $"store-login-{culture}-{width}.png");
            await page.Locator("[name=UserName]").FillAsync("trial-owner");
            await page.Locator("[name=Password]").FillAsync("Trial-Owner!Strong-2026");
            await page.Locator("form:has([name=UserName]) [type=submit]").ClickAsync();
            await page.WaitForURLAsync(origin + storeUrl);
            Assert.Equal(name, await page.Locator(".store-context-banner strong").InnerTextAsync());
            Assert.Equal(0, await page.Locator("a[href$='/Stores']").CountAsync());
            await page.GotoAsync(origin + storeUrl + "StoreSettings");
            Assert.Equal(name, await page.Locator(".store-profile-card .document-business-name").InnerTextAsync());
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth"));
            await Shot(page, $"store-owner-{culture}-{width}.png");
        }
        finally
        {
            await app.DisposeAsync();
            if(operation != Guid.Empty)
            {
                var options = new NpgsqlConnectionStringBuilder(fixture.TestConnection) { Database = "postgres", Pooling = false };
                await using var admin = new NpgsqlConnection(options.ConnectionString);
                await admin.OpenAsync();
                var expected = "nts_trial_" + operation.ToString("N");
                Assert.Equal(expected, StoreServerSettings.DatabaseName(operation));
                await new NpgsqlCommand("DROP DATABASE IF EXISTS " + new NpgsqlCommandBuilder().QuoteIdentifier(expected) + " WITH (FORCE)", admin).ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task Shot(IPage page, string name)
    {
        var qa = Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");
        if(string.IsNullOrWhiteSpace(qa)) return;
        Directory.CreateDirectory(qa);
        await page.ScreenshotAsync(new() { Path = Path.Combine(qa, name), FullPage = true, Animations = ScreenshotAnimations.Disabled, Style = ".notification-stack{visibility:hidden!important;}" });
    }
}
