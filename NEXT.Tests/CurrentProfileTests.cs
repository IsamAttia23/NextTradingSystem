using NEXT.Infrastructure.Identity.Entities;
using System.Net;
using System.Globalization;
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
        Assert.Contains("href=\"/Account/Profile\"", html);
        Assert.Contains("data-modal-url=\"/Account/EditProfile\"", html);
        Assert.Contains("UI test ui-reader", html);
        Assert.Contains("ui-reader@example.test", html);
        Assert.Contains("profile-details", html);
        Assert.DoesNotContain("name=\"CurrentPassword\"", html);
        html = await client.GetStringAsync("/Account/EditProfile");
        Assert.Contains("data-modal-form=\"true\"", html);
        Assert.Contains("data-profile-upload", html);
        Assert.Contains("profile-picker-circle", html);
        Assert.DoesNotContain("id=\"workspaceModal\"", html);
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
        Assert.Equal("/Account/Profile", json.RootElement.GetProperty("redirectUrl").GetString());
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
        Assert.Contains("Updated reader", html);
        Assert.Contains("src=\"/Account/Avatar\"", html);
        response = await WorkspaceUiTests.Post(client, "/Account/Profile", WorkspaceUiTests.Token(html),
            ("FullName", "Preserved photo"), ("UserName", "ui-reader"), ("Email", "ui-reader@example.test"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(avatar, await client.GetByteArrayAsync("/Account/Avatar"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfileShowsRecordedLoginHistoryAfterAuthenticationCompletes(bool useTwoFactor)
    {
        var firstLogin = new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);
        var clock = new ProfileClock { UtcNow = firstLogin.AddMinutes(-10) };
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: services =>
            services.AddSingleton<TimeProvider>(clock));
        using var firstClient = WorkspaceUiTests.Client(app);
        using var secondClient = WorkspaceUiTests.Client(app);
        var recoveryCodes = Array.Empty<string>();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByNameAsync("ui-reader"))!;
            Assert.Null(user.LastLogin);
            if (useTwoFactor)
            {
                user.EmailConfirmed = true;
                Assert.True((await users.UpdateAsync(user)).Succeeded);
                Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);
                Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);
                recoveryCodes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 2))!.ToArray();
            }
        }

        var login = await firstClient.GetStringAsync("/Account/Login");
        var failed = await WorkspaceUiTests.Post(firstClient, "/Account/Login", WorkspaceUiTests.Token(login),
            ("UserName", "ui-reader"), ("Password", "Wrong!12345"));
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);

        clock.UtcNow = firstLogin;
        await SignIn(firstClient, 0, null);
        var firstSession = await Profile(firstClient);
        Assert.Contains("Last sign-in <bdi>—</bdi>", firstSession);

        clock.UtcNow = firstLogin.AddHours(4);
        await SignIn(secondClient, 1, firstLogin.UtcDateTime);
        var secondSession = await Profile(secondClient);
        var expectedPrevious = firstLogin.LocalDateTime.ToString("g", CultureInfo.GetCultureInfo("en"));
        var expectedFailure = firstLogin.AddMinutes(-10).LocalDateTime.ToString("g", CultureInfo.GetCultureInfo("en"));
        Assert.Contains($"Last sign-in <bdi>{expectedPrevious}</bdi>", secondSession);
        Assert.Contains($"Last failed sign-in attempt <bdi>{expectedFailure}</bdi>", secondSession);
        var details = secondSession.Split("class=\"detail-grid profile-details\"", StringSplitOptions.None)[1];
        Assert.Contains($"<bdi>{expectedPrevious}</bdi>", details);
        Assert.DoesNotContain(clock.UtcNow.LocalDateTime.ToString("g", CultureInfo.GetCultureInfo("en")), secondSession);
        Assert.Contains("Last sign-in <bdi>—</bdi>", await Profile(firstClient));
        Assert.Equal(clock.UtcNow.UtcDateTime, await ReadLastLogin());

        static async Task<string> Profile(HttpClient client) =>
            WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Profile?culture=en"));

        async Task<DateTime?> ReadLastLogin()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            return await db.Users.Where(user => user.UserName == "ui-reader").Select(user => user.LastLogin).SingleAsync();
        }

        async Task SignIn(HttpClient client, int codeIndex, DateTime? previousLogin)
        {
            if (!useTwoFactor)
            {
                await WorkspaceUiTests.Login(client, "ui-reader");
                return;
            }
            var form = await client.GetStringAsync("/Account/Login");
            var result = await WorkspaceUiTests.Post(client, "/Account/Login", WorkspaceUiTests.Token(form),
                ("UserName", "ui-reader"), ("Password", "Test-only!2026"));
            Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
            Assert.Contains("/Account/Verify", result.Headers.Location!.OriginalString);
            Assert.Equal(previousLogin, await ReadLastLogin());
            var verify = await client.GetStringAsync("/Account/Verify");
            var verified = await WorkspaceUiTests.Post(client, "/Account/Verify", WorkspaceUiTests.Token(verify),
                ("Code", recoveryCodes[codeIndex]), ("UseRecoveryCode", "true"));
            Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        }
    }

    private sealed class ProfileClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    [Fact]
    public async Task ProfileRejectsInvalidRequestsWithHttpErrorsAndRequiresAuthenticationAndAntiforgery()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var anonymous = WorkspaceUiTests.Client(app);
        anonymous.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/Account/Profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/Account/EditProfile")).StatusCode);
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
