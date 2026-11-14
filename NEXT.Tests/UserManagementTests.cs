using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using NEXT.Infrastructure.Identity.Entities;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class UserManagementTests
{
    [Fact]
    public async Task LockedAccountCanBeRestoredFromItsActivationSwitch()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var admin = WorkspaceUiTests.Client(app);
        using var reader = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(admin, "ui-admin");
        var loginToken = WorkspaceUiTests.Token(await reader.GetStringAsync("/Account/Login"));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(reader, "/Account/Login", loginToken,
                ("UserName", "ui-reader"), ("Password", "Wrong!12345"))).StatusCode);
        }
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var target = await db.Users.AsNoTracking().SingleAsync(x => x.UserName == "ui-reader");
        Assert.True(target.IsActive);
        Assert.True(target.RequiresAdministratorUnlock);
        var html = await admin.GetStringAsync("/Users?culture=en");
        Assert.Contains("data-locked=\"true\"", html);
        var token = WorkspaceUiTests.Token(html);
        var path = $"/Users/SetActive/{target.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await WorkspaceUiTests.Post(admin, path, token,
            ("IsActive", "false"), ("ExpectedIsActive", "true"), ("ExpectedIsLocked", "false"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, path, token,
            ("IsActive", "true"), ("ExpectedIsActive", "true"), ("ExpectedIsLocked", "true"))).StatusCode);
        target = await db.Users.AsNoTracking().SingleAsync(x => x.Id == target.Id);
        Assert.False(target.RequiresAdministratorUnlock);
        Assert.Equal(0, target.AccessFailedCount);
        Assert.Null(target.LockoutEnd);
        await WorkspaceUiTests.Login(reader, "ui-reader");
    }

    [Fact]
    public async Task RoleDropdownCreatesAndReplacesMultipleRolesAndRejectsStaleChanges()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var admin = WorkspaceUiTests.Client(app);
        using var reader = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(admin, "ui-admin");
        await WorkspaceUiTests.Login(reader, "ui-reader");
        await using var setup = app.Services.CreateAsyncScope();
        var roles = setup.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        foreach (var name in new[] { "Sales", "Warehouse", "Accounting" })
        {
            Assert.True((await roles.CreateAsync(new Role { Name = name })).Succeeded);
        }
        var db = setup.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var target = await db.Users.AsNoTracking().SingleAsync(x => x.UserName == "ui-reader");
        var create = await admin.GetStringAsync("/Users/Create?culture=en");
        Assert.Contains("data-bs-auto-close=\"outside\"", create);
        Assert.Contains("name=\"Roles\"", create);
        var created = await WorkspaceUiTests.Post(admin, "/Users/Create", WorkspaceUiTests.Token(create),
            ("FullName", "Multiple roles"), ("UserName", "multi-role"),
            ("Email", "multi-role@example.test"), ("BranchId", target.BranchId.ToString()),
            ("Roles", "Sales"), ("Roles", "Warehouse"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var createdId = await db.Users.Where(x => x.UserName == "multi-role").Select(x => x.Id).SingleAsync();
        Assert.Equal(new[] { "Sales", "Warehouse" }, await Assigned(createdId));

        var assign = $"/Users/AssignRole/{createdId}";
        var edit = await admin.GetStringAsync(assign);
        Assert.Matches("value=\"Sales\" checked=", edit);
        Assert.Matches("value=\"Warehouse\" checked=", edit);
        var token = WorkspaceUiTests.Token(edit);
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, assign, token,
            ("RoleSelection", "true"), ("ExpectedRoles", "Sales"), ("ExpectedRoles", "Warehouse"),
            ("Roles", "Sales"), ("Roles", "Accounting"))).StatusCode);
        Assert.Equal(new[] { "Accounting", "Sales" }, await Assigned(createdId));
        Assert.Equal(HttpStatusCode.Conflict, (await WorkspaceUiTests.Post(admin, assign, token,
            ("RoleSelection", "true"), ("ExpectedRoles", "Sales"), ("ExpectedRoles", "Warehouse"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await WorkspaceUiTests.Post(admin, assign, token,
            ("RoleSelection", "true"), ("ExpectedRoles", "Accounting"), ("ExpectedRoles", "Sales"), ("Roles", "Missing"))).StatusCode);
        Assert.Equal(new[] { "Accounting", "Sales" }, await Assigned(createdId));
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, assign, token,
            ("RoleSelection", "true"), ("ExpectedRoles", "Accounting"), ("ExpectedRoles", "Sales"))).StatusCode);
        Assert.Empty(await Assigned(createdId));

        var administratorId = await db.Users.Where(x => x.UserName == "ui-admin").Select(x => x.Id).SingleAsync();
        var removeAdmin = await WorkspaceUiTests.Post(admin, $"/Users/AssignRole/{administratorId}", token,
            ("RoleSelection", "true"), ("ExpectedRoles", "Admin"));
        Assert.Equal(HttpStatusCode.BadRequest, removeAdmin.StatusCode);
        Assert.Contains("Admin", await Assigned(administratorId));

        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, $"/Users/AssignRole/{target.Id}", token,
            ("RoleSelection", "true"), ("Roles", "Sales"))).StatusCode);
        reader.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/Account/Profile")).StatusCode);

        async Task<string[]> Assigned(Guid userId)
        {
            await using var read = app.Services.CreateAsyncScope();
            var users = read.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = (await users.FindByIdAsync(userId.ToString()))!;
            return (await users.GetRolesAsync(account)).Order().ToArray();
        }
    }

    [Theory]
    [InlineData("ar", "الحساب مقفل")]
    [InlineData("en", "Your account is locked")]
    public async Task AdminDeactivationShowsLockedPageAfterCorrectCredentials(string language, string heading)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var admin = WorkspaceUiTests.Client(app);
        using var reader = WorkspaceUiTests.Client(app);
        reader.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        await WorkspaceUiTests.Login(admin, "ui-admin");
        Guid id;
        await using (var scope = app.Services.CreateAsyncScope())
            id = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Users
                .Where(x => x.UserName == "ui-reader").Select(x => x.Id).SingleAsync();
        var list = await admin.GetStringAsync("/Users");
        var token = WorkspaceUiTests.Token(list);
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, $"/Users/SetActive/{id}", token,
            ("IsActive", "false"), ("ExpectedIsActive", "true"))).StatusCode);
        var login = await reader.GetStringAsync("/Account/Login");
        var wrong = await WorkspaceUiTests.Post(reader, "/Account/Login", WorkspaceUiTests.Token(login),
            ("UserName", "ui-reader"), ("Password", "Incorrect!12345"));
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Null(wrong.Headers.Location);
        var blocked = await WorkspaceUiTests.Post(reader, "/Account/Login", WorkspaceUiTests.Token(login),
            ("UserName", "ui-reader"), ("Password", "Test-only!2026"));
        Assert.Equal(HttpStatusCode.Redirect, blocked.StatusCode);
        Assert.Equal("/Account/Locked", blocked.Headers.Location!.OriginalString);
        var locked = WebUtility.HtmlDecode(await reader.GetStringAsync("/Account/Locked"));
        Assert.Contains(heading, locked);
        Assert.DoesNotContain(language == "ar" ? "٣ محاولات" : "three unsuccessful", locked);
        Assert.Equal(HttpStatusCode.Redirect, (await reader.GetAsync("/Account/Profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, $"/Users/SetActive/{id}", token,
            ("IsActive", "true"), ("ExpectedIsActive", "false"))).StatusCode);
        await WorkspaceUiTests.Login(reader, "ui-reader");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task CurrentUserIsHiddenAndCannotDeactivateSelfOrUseEditToChangeStatus()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        Guid id, branch;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Users.SingleAsync(x => x.UserName == "ui-admin");
            id = user.Id; branch = user.BranchId;
        }
        var list = await client.GetStringAsync("/Users");
        Assert.DoesNotContain($"/Users/Edit/{id}", list);
        Assert.DoesNotContain($"/Users/SetActive/{id}", list);
        Assert.Contains("data-user-activation", list);
        var response = await WorkspaceUiTests.Post(client, $"/Users/SetActive/{id}", WorkspaceUiTests.Token(list),
            ("IsActive", "false"), ("ExpectedIsActive", "true"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var edit = await client.GetStringAsync($"/Users/Edit/{id}");
        Assert.DoesNotContain("name=\"IsActive\"", edit);
        response = await WorkspaceUiTests.Post(client, $"/Users/Edit/{id}", WorkspaceUiTests.Token(edit),
            ("FullName", "UI test ui-admin"), ("UserName", "ui-admin"), ("Email", "ui-admin@example.test"),
            ("BranchId", branch.ToString()), ("IsActive", "false"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var check = app.Services.CreateAsyncScope();
        Assert.True((await check.ServiceProvider.GetRequiredService<NEXTDbContext>().Users.SingleAsync(x => x.Id == id)).IsActive);
    }

    [Fact]
    public async Task ActivationSwitchRequiresAdminCsrfAndCurrentStateAndInvalidatesSessions()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var admin = WorkspaceUiTests.Client(app);
        using var reader = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(admin, "ui-admin");
        await WorkspaceUiTests.Login(reader, "ui-reader");
        Guid id;
        await using (var scope = app.Services.CreateAsyncScope())
            id = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Users.Where(x => x.UserName == "ui-reader").Select(x => x.Id).SingleAsync();
        var list = await admin.GetStringAsync("/Users");
        var token = WorkspaceUiTests.Token(list);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/Users/SetActive/{id}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["IsActive"] = "false", ["ExpectedIsActive"] = "true" }))).StatusCode);
        reader.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var profile = await reader.GetStringAsync("/Account/Profile");
        Assert.Equal(HttpStatusCode.Forbidden, (await WorkspaceUiTests.Post(reader, $"/Users/SetActive/{id}",
            WorkspaceUiTests.Token(profile), ("IsActive", "false"), ("ExpectedIsActive", "true"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, $"/Users/SetActive/{id}", token,
            ("IsActive", "false"), ("ExpectedIsActive", "true"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/Account/Profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await WorkspaceUiTests.Post(admin, $"/Users/SetActive/{id}", token,
            ("IsActive", "false"), ("ExpectedIsActive", "true"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(admin, $"/Users/SetActive/{id}", token,
            ("IsActive", "true"), ("ExpectedIsActive", "false"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/Account/Profile")).StatusCode);
        await WorkspaceUiTests.Login(reader, "ui-reader");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/Account/Profile")).StatusCode);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public async Task DeletedUsersAppearInRecycleBinCanBeRestoredInactiveAndPurged(string language)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        await WorkspaceUiTests.Login(client, "ui-admin");
        Guid id;
        await using (var scope = app.Services.CreateAsyncScope())
            id = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Users.Where(x => x.UserName == "ui-reader").Select(x => x.Id).SingleAsync();
        var list = await client.GetStringAsync("/Users");
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, $"/Users/Delete/{id}", WorkspaceUiTests.Token(list),
            ("Reason", "Test deletion"))).StatusCode);
        list = await client.GetStringAsync("/Users");
        Assert.DoesNotContain($"/Users/Edit/{id}", list);
        var recycle = await client.GetStringAsync("/RecycleBin?section=ApplicationUser");
        Assert.Contains("ui-reader", recycle);
        Assert.Contains("Test deletion", recycle);
        Assert.Contains(id.ToString(), recycle);
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/RecycleBin/Restore", WorkspaceUiTests.Token(recycle),
            ("section", "ApplicationUser"), ("id", id.ToString()))).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Users.SingleAsync(x => x.Id == id);
            Assert.Null(user.DeletedAt);
            Assert.False(user.IsActive);
        }
        list = await client.GetStringAsync("/Users");
        Assert.Contains($"/Users/SetActive/{id}", list);
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, $"/Users/Delete/{id}", WorkspaceUiTests.Token(list),
            ("Reason", "Purge test"))).StatusCode);
        recycle = await client.GetStringAsync("/RecycleBin?section=ApplicationUser");
        Assert.Equal(HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/RecycleBin/Purge", WorkspaceUiTests.Token(recycle),
            ("section", "ApplicationUser"), ("id", id.ToString()), ("Reason", "Permanent deletion test"))).StatusCode);
        await using var check = app.Services.CreateAsyncScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<NEXTDbContext>().Users.IgnoreQueryFilters().AnyAsync(x => x.Id == id));
    }
}
