using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Services;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;
namespace NEXT.Tests;
public sealed class ProductReportFinanceBrowserTests
{
    [PdfTheory]
    [InlineData("ar",390)] [InlineData("ar",1280)] [InlineData("en",390)] [InlineData("en",1280)]
    public async Task CategorizedReportPdfAndFinancialControlsWorkWithoutHorizontalScrolling(string culture,int width)
    {
        var actor=new AccountingFoundationTests.TestActor();
        await using var app=await WorkspaceUiTests.StartAsync(configureServices:s=>s.AddSingleton<ICurrentUserService>(actor));
        Guid categoryId;
        await using(var scope=app.Services.CreateAsyncScope()) {
            var db=await AccountingFoundationTests.Activate(scope.ServiceProvider,actor);
            var c=new Category{Name="أجهزة منزلية / Appliances"}; categoryId=c.Id=Guid.NewGuid();
            db.AddRange(Enumerable.Range(1,24).Select(i=>new Product{Name="منتج تجريبي / Product "+i,Category=c,CostPrice=10,RetailPrice=20,WholesalePrice=10}));
            db.Add(new Product{Name="Excluded from report",CostPrice=50});await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IAccountingPostingService>().PostAsync(AccountingFoundationTests.TreasuryOpening(100),default);
        }
        var browser=await app.Services.GetRequiredService<InvoicePdfBrowser>().GetAsync(default);
        await using var context=await browser.NewContextAsync(new(){Locale=culture,ViewportSize=new(){Width=width,Height=1000}});
        var page=await context.NewPageAsync();page.SetDefaultTimeout(20000);
        using var client=WorkspaceUiTests.Client(app);var origin=client.BaseAddress!.ToString().TrimEnd('/');
        await page.GotoAsync(origin+"/Account/Login?culture="+culture);
        await page.Locator("[name=UserName]").FillAsync("ui-admin");await page.Locator("[name=Password]").FillAsync("Test-only!2026");
        await page.Locator("form:has([name=UserName]) button[type=submit]").ClickAsync();await page.WaitForURLAsync(origin+"/");
        await page.GotoAsync(origin+"/Products?CategoryId="+categoryId+"&PageSize=10&culture="+culture);
        await page.Locator("a[href*='/ProductReports/Print']").ClickAsync();
        Assert.Equal(24,await page.Locator(".report tbody tr").CountAsync());Assert.Equal(1,await page.Locator(".report h2").CountAsync());
        Assert.DoesNotContain("Excluded",await page.Locator(".report").InnerTextAsync());
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await Shot(page,$"products-{culture}-{width}.png");
        var download=await page.RunAndWaitForDownloadAsync(()=>page.Locator("a[href*='/ProductReports/Pdf']").ClickAsync());
        var path=await download.PathAsync();Assert.NotNull(path);var bytes=await File.ReadAllBytesAsync(path!);Assert.True(bytes.Length>1000);Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(bytes,0,5));
        var qa=Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");if(!string.IsNullOrWhiteSpace(qa)) await download.SaveAsAsync(Path.Combine(qa,$"products-{culture}-{width}.pdf"));
        foreach(var route in new[]{"/FinancialAccounts/Movements?Account=Treasury","/FinancialAccounts/Details?account=Treasury","/FinancialAccounts/Reconciliation","/FinancialControls/Periods","/FinancialControls/Transfers","/FinancialControls/Reconciliations"}) {
            await page.GotoAsync(origin+route+(route.Contains('?')?"&":"?")+"culture="+culture);
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
            Assert.True(await page.Locator(".table-responsive").EvaluateAllAsync<bool>("els => els.every(e => e.scrollWidth <= e.clientWidth + 1)"));
            await Shot(page,$"finance-{route.Split('?')[0].Split('/').Last()}-{culture}-{width}.png");
        }
        await page.GotoAsync(origin+"/FinancialControls/Transfers?culture="+culture);
        await page.Locator("[data-modal-url*='/Transfer']").ClickAsync();var modal=page.Locator("#workspaceModal.show");
        await modal.Locator("[name=Amount]").FillAsync("40");await modal.Locator("[name=Date]").FillAsync("2026-11-21");await modal.Locator("[name=Reason]").FillAsync("Deposit / إيداع");
        await modal.Locator("[type=submit]").ClickAsync();await page.Locator("#workspaceModal").WaitForAsync(new(){State=WaitForSelectorState.Hidden});Assert.Equal(1,await page.Locator("tbody tr").CountAsync());
        await page.GotoAsync(origin+"/FinancialControls/Reconciliations?culture="+culture);
        await page.Locator("[name=account]").SelectOptionAsync("Bank");await page.Locator("[name=date]").FillAsync("2026-11-21");await page.Locator("[data-reconciliation-preview] [type=submit]").ClickAsync();
        await modal.Locator("[name=ExternalBalance]").FillAsync("40");await modal.Locator("[name=Evidence]").FillAsync("Statement 1 / كشف 1");await modal.Locator("[name=Reason]").FillAsync("Reviewed / تمت المراجعة");
        await modal.Locator("[type=submit]").ClickAsync();await page.Locator("#workspaceModal").WaitForAsync(new(){State=WaitForSelectorState.Hidden});Assert.Equal(1,await page.Locator("tbody tr").CountAsync());
        await page.GotoAsync(origin+"/FinancialControls/Periods?culture="+culture);await page.Locator("[data-modal-url*='CreatePeriod']").ClickAsync();
        await modal.Locator("[name=Name]").FillAsync("November / نوفمبر");await modal.Locator("[name=Start]").FillAsync("2026-11-01");await modal.Locator("[name=End]").FillAsync("2026-11-21");await modal.Locator("[name=Reason]").FillAsync("Opening period / فتح الفترة");await modal.Locator("[type=submit]").ClickAsync();await page.Locator("#workspaceModal").WaitForAsync(new(){State=WaitForSelectorState.Hidden});
        for(var i=0;i<2;i++) {await page.Locator("[data-modal-url*='ChangePeriod']").ClickAsync();await modal.Locator("[name=Reason]").FillAsync("Approved / معتمد");var responseTask=page.WaitForResponseAsync(r=>r.Url.Contains("/ChangePeriod") && r.Request.Method=="POST");await modal.Locator("[type=submit]").ClickAsync();var response=await responseTask;Assert.True(response.Ok,await response.TextAsync());await page.Locator("#workspaceModal").WaitForAsync(new(){State=WaitForSelectorState.Hidden});}
        await page.Locator("details summary").ClickAsync();await Shot(page,$"period-audit-{culture}-{width}.png");
        await using var finalScope=app.Services.CreateAsyncScope();var final=finalScope.ServiceProvider.GetRequiredService<NEXTDbContext>();Assert.Equal(3,await final.FinancialPeriodEvents.CountAsync());Assert.False((await final.FinancialPeriods.SingleAsync()).IsClosed);Assert.Equal(1,await final.CashTransfers.CountAsync());
    }
    private static async Task Shot(IPage page,string name) {var root=Environment.GetEnvironmentVariable("NEXT_PDF_QA_DIRECTORY");if(string.IsNullOrWhiteSpace(root))return;Directory.CreateDirectory(root);await page.ScreenshotAsync(new(){Path=Path.Combine(root,name),FullPage=true,Animations=ScreenshotAnimations.Disabled});}
}
