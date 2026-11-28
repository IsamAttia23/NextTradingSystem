using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Features.Settings;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class StoreProfileTests
{
    [Fact]
    public async Task StoreDetailsRequireAuthorizationValidateInputAndRejectStaleEdits()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var anonymous = WorkspaceUiTests.Client(app);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/StoreSettings")).StatusCode);
        using var reader = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(reader, "ui-reader");
        using (var denied = await reader.GetAsync("/StoreSettings"))
        {
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
            Assert.Contains("/Account/Denied", denied.Headers.Location!.ToString());
        }
        using var admin = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(admin, "ui-admin");
        var editor = await admin.GetStringAsync("/StoreSettings/Edit");
        var token = WorkspaceUiTests.Token(editor);
        using (var missingToken = await admin.PostAsync("/StoreSettings/Edit", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "QA Store" })))
            Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        using (var invalid = await WorkspaceUiTests.Post(admin, "/StoreSettings/Edit", token, ("Name", new string('x', 201))))
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        const string name = "متجر الاختبار / QA Store";
        using (var saved = await WorkspaceUiTests.Post(admin, "/StoreSettings/Edit", token,
            ("Name", "  " + name + "  "), ("Address", "الخرطوم <script>alert('address')</script>"), ("Phone", "+249123456789")))
            Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync());
        await using var scope = app.Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<StoreProfileSettings>();
        var profile = await settings.ReadAsync(default);
        Assert.Equal(name, profile.Name);
        Assert.NotEmpty(profile.RowVersion!);
        var version = Convert.ToBase64String(profile.RowVersion!);
        var page = await admin.GetStringAsync("/StoreSettings");
        Assert.Contains(name, WebUtility.HtmlDecode(page));
        Assert.DoesNotContain("<script>alert('address')</script>", page);
        using (var updated = await WorkspaceUiTests.Post(admin, "/StoreSettings/Edit", token,
            ("Name", name + " Updated"), ("RowVersion", version)))
            Assert.True(updated.IsSuccessStatusCode, await updated.Content.ReadAsStringAsync());
        using (var stale = await WorkspaceUiTests.Post(admin, "/StoreSettings/Edit", token,
            ("Name", "Stale override"), ("RowVersion", version)))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        Assert.Equal(1, await db.SystemSettings.CountAsync(s => s.Key == StoreProfileSettings.SettingKey));
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }
}
