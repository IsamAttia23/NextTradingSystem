using NEXT.Infrastructure.Identity.Entities;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class CurrentProfileTests
{
    [Fact]
    public async Task NonAdministratorCanEditOwnProfileAndUploadPhotoWithoutChangingProtectedFields()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-reader");
        var html = await client.GetStringAsync("/Account/Profile");
        Assert.Contains("data-profile-form=\"true\"", html);
        Assert.Contains("href=\"/Account/Profile\"", html);
        foreach (var field in new[] { "FullName", "UserName", "Email", "PhoneNumber", "Avatar", "CurrentPassword", "NewPassword", "ConfirmPassword" })
        {
            Assert.Contains("name=\"" + field + "\"", html);
        }
        foreach (var field in new[] { "BranchId", "IsActive", "Role", "Roles", "UserId" })
        {
            Assert.DoesNotContain("name=\"" + field + "\"", html);
        }
        Guid originalBranch, otherUserId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            originalBranch = (await db.Users.SingleAsync(x => x.UserName == "ui-reader")).BranchId;
            otherUserId = (await db.Users.SingleAsync(x => x.UserName == "ui-admin")).Id;
        }
        var image = await File.ReadAllBytesAsync(Path.Combine(app.Environment.ContentRootPath, "wwwroot/pwa/icon-192.png"));
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WorkspaceUiTests.Token(html),
            ["FullName"] = "Updated reader",
            ["UserName"] = "ui-reader",
            ["Email"] = "ui-reader@example.test",
            ["BranchId"] = Guid.NewGuid().ToString(),
            ["IsActive"] = "false",
            ["Roles"] = "Admin",
            ["UserId"] = otherUserId.ToString(),
            ["ProfileOnly"] = "false"
        })
        {
            form.Add(new StringContent(field.Value), field.Key);
        }
        form.Add(new ByteArrayContent(image), "Avatar", "photo.png");
        var response = await client.PostAsync("/Account/Profile", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("success", json.RootElement.GetProperty("notification").GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("redirectUrl").ValueKind);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByNameAsync("ui-reader"))!;
            Assert.Equal("Updated reader", user.FullName);
            Assert.Equal(originalBranch, user.BranchId);
            Assert.True(user.IsActive);
            Assert.Empty(await users.GetRolesAsync(user));
            Assert.NotEmpty(user.AvatarUrl!);
            Assert.True(await users.CheckPasswordAsync(user, "Test-only!2026"));
            Assert.Equal("UI test ui-admin", (await users.FindByNameAsync("ui-admin"))!.FullName);
        }
        var avatar = await client.GetByteArrayAsync("/Account/Avatar");
        html = await client.GetStringAsync("/Account/Profile");
        response = await WorkspaceUiTests.Post(client, "/Account/Profile", WorkspaceUiTests.Token(html),
            ("FullName", "Preserved photo"), ("UserName", "ui-reader"), ("Email", "ui-reader@example.test"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(avatar, await client.GetByteArrayAsync("/Account/Avatar"));
    }

    [Fact]
    public async Task ProfileRejectsInvalidRequestsWithHttpErrorsAndRequiresAuthenticationAndAntiforgery()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var anonymous = WorkspaceUiTests.Client(app);
        anonymous.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/Account/Profile")).StatusCode);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-reader");
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var token = WorkspaceUiTests.Token(await client.GetStringAsync("/Account/Profile"));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync("/Account/Profile", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        foreach (var scenario in new[]
        {
            new[] { ("Email", "invalid-email") },
            new[] { ("Email", "ui-admin@example.test") },
            new[] { ("NewPassword", "Changed-only!2026"), ("ConfirmPassword", "different-password") },
            new[] { ("NewPassword", "Changed-only!2026"), ("ConfirmPassword", "Changed-only!2026") },
            new[] { ("CurrentPassword", "wrong-password"), ("NewPassword", "Changed-only!2026"), ("ConfirmPassword", "Changed-only!2026") }
        })
        {
            var fields = new Dictionary<string, string>
            {
                ["FullName"] = "UI test ui-reader",
                ["UserName"] = "ui-reader",
                ["Email"] = "ui-reader@example.test"
            };
            foreach (var (key, value) in scenario)
                fields[key] = value;
            var response = await WorkspaceUiTests.Post(client, "/Account/Profile", token,
                fields.Select(x => (x.Key, x.Value)).ToArray());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.TryGetProperty("errors", out _));
        }
        using var invalidImage = new MultipartFormDataContent();
        invalidImage.Add(new StringContent(token), "__RequestVerificationToken");
        invalidImage.Add(new StringContent("UI test ui-reader"), "FullName");
        invalidImage.Add(new StringContent("ui-reader"), "UserName");
        invalidImage.Add(new StringContent("ui-reader@example.test"), "Email");
        invalidImage.Add(new ByteArrayContent([1, 2, 3]), "Avatar", "fake.png");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Profile", invalidImage)).StatusCode);
    }

    [Fact]
    public async Task OneProfileRequestUpdatesAccountAndPasswordThenSignsOut()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-reader");
        var token = WorkspaceUiTests.Token(await client.GetStringAsync("/Account/Profile"));
        var response = await WorkspaceUiTests.Post(client, "/Account/Profile", token,
            ("FullName", "Updated account"), ("UserName", "reader-updated"),
            ("Email", "updated@example.test"), ("PhoneNumber", "+249123456789"),
            ("CurrentPassword", "Test-only!2026"), ("NewPassword", "Changed-only!2026"),
            ("ConfirmPassword", "Changed-only!2026"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.StartsWith("/Account/Login", json.RootElement.GetProperty("redirectUrl").GetString());
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/Account/Profile")).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync("reader-updated"))!;
        Assert.Equal("Updated account", user.FullName);
        Assert.Equal("updated@example.test", user.Email);
        Assert.Equal("+249123456789", user.PhoneNumber);
        Assert.False(user.EmailConfirmed);
        Assert.False(user.PhoneNumberConfirmed);
        Assert.True(await users.CheckPasswordAsync(user, "Changed-only!2026"));
        Assert.False(await users.CheckPasswordAsync(user, "Test-only!2026"));
    }

    [PostgresFact]
    public async Task FailedPasswordChangeRollsBackOtherProfileChanges()
    {
        await using var database = await PostgresInventoryTests.Database.Create();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: database.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-reader");
        var token = WorkspaceUiTests.Token(await client.GetStringAsync("/Account/Profile"));
        var response = await WorkspaceUiTests.Post(client, "/Account/Profile", token,
            ("FullName", "Must roll back"), ("UserName", "must-rollback"),
            ("Email", "rollback@example.test"), ("PhoneNumber", "+249123456789"),
            ("CurrentPassword", "wrong-password"), ("NewPassword", "Changed-only!2026"),
            ("ConfirmPassword", "Changed-only!2026"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByNameAsync("ui-reader"))!;
        Assert.NotNull(user);
        Assert.Equal("UI test ui-reader", user.FullName);
        Assert.Equal("ui-reader@example.test", user.Email);
        Assert.Null(user.PhoneNumber);
        Assert.True(await users.CheckPasswordAsync(user, "Test-only!2026"));
        Assert.Null(await users.FindByNameAsync("must-rollback"));
    }
}
