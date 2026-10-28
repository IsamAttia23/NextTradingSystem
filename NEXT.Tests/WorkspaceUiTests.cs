using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NEXT.Application;
using NEXT.Domain.Entities;
using NEXT.Infrastructure;
using NEXT.Infrastructure.Persistence;
using NEXT.web;
using NEXT.web.Controllers;
using NEXT.web.Middleware;
using Xunit;

namespace NEXT.Tests;

/// <summary>Real HTTP, cookies, Razor, antiforgery and application handlers; isolated in-memory storage.</summary>
public sealed class WorkspaceUiTests
{
    public static async Task<WebApplication> StartAsync(string? contentRoot = null)
    {
        var webRoot = contentRoot ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../NEXT.web"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(HomeController).Assembly.GetName().Name,
            ContentRootPath = webRoot,
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:NEXTDatabase"] = "Host=localhost;Database=unused"
        });
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.RemoveAll<DbContextOptions<NEXTDbContext>>();
        builder.Services.RemoveAll<IDbContextOptionsConfiguration<NEXTDbContext>>();
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<NEXTDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        }).AddIdentityCookies();
        builder.Services.AddPresentation(builder.Configuration);
        var app = builder.Build();
        app.UseStatusCodePagesWithReExecute("/Status/{0}");
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseRequestLocalization();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var branch = new Branch { Id = Guid.NewGuid(), Name = "Khartoum / الخرطوم", IsActive = true };
            db.Add(branch);
            await db.SaveChangesAsync();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            Assert.True((await roles.CreateAsync(new Role { Id = Guid.NewGuid(), Name = "Admin", IsSystem = true })).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var name in new[] { "ui-admin", "ui-reader" })
            {
                var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = name, FullName = "UI test " + name,
                    Email = name + "@example.test", IsActive = true, BranchId = branch.Id };
                Assert.True((await users.CreateAsync(user, "Test-only!2026")).Succeeded);
                if (name == "ui-admin") Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
            }
        }
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, CookieContainer = new CookieContainer()
    }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };

    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    private static Task<HttpResponseMessage> Post(HttpClient client, string url, string token, params (string Key, string Value)[] fields) =>
        client.PostAsync(url, new FormUrlEncodedContent(fields.Append(("__RequestVerificationToken", token))
            .Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2))));

    private static async Task Login(HttpClient client, string name)
    {
        var html = await client.GetStringAsync("/Account/Login");
        var result = await Post(client, "/Account/Login", Token(html), ("UserName", name), ("Password", "Test-only!2026"), ("ReturnUrl", "https://example.org/evil"));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal("/", result.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task WorkspaceSupportsCultureAuthorizationAntiforgeryAndCrud()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Roles")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/Roles/Create")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Requested-With");
        var arabic = await client.GetStringAsync("/Account/Login");
        Assert.Contains("dir=\"rtl\"", arabic);
        Assert.Contains("bootstrap.rtl.min.css", arabic);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Culture/Set", new FormUrlEncodedContent(new Dictionary<string,string> { ["culture"] = "en" }))).StatusCode);
        await Post(client, "/Culture/Set", Token(arabic), ("culture", "en"), ("returnUrl", "https://example.org/evil"));
        var english = await client.GetStringAsync("/Account/Login");
        Assert.Contains("dir=\"ltr\"", english);
        Assert.Contains("Sign in", english);
        await Login(client, "ui-admin");
        foreach (var page in new[] { "/", "/Users", "/Roles", "/Permissions", "/Assignments", "/RecycleBin?section=Tag" })
        {
            var response = await client.GetAsync(page);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("workspaceModal", await response.Content.ReadAsStringAsync());
        }
        var form = await client.GetStringAsync("/Roles/Create");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Roles/Create", new FormUrlEncodedContent(new Dictionary<string,string> { ["Name"] = "Forbidden" }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Roles/Create", Token(form), ("Name", "Sales"))).StatusCode);
        var rolesHtml = await client.GetStringAsync("/Roles");
        Assert.Contains("Sales", rolesHtml);
        Guid roleId;
        await using (var scope = app.Services.CreateAsyncScope())
            roleId = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Roles.Where(x => x.Name == "Sales").Select(x => x.Id).SingleAsync();
        var edit = await client.GetStringAsync("/Roles/Edit/" + roleId);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Roles/Edit/" + roleId, Token(edit), ("Name", "Sales updated"))).StatusCode);
        var permissionForm = await client.GetStringAsync("/Permissions/Create");
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/Permissions/Create", Token(permissionForm), ("Code", ""), ("Module", "Sales"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Permissions/Create", Token(permissionForm), ("Code", "Sales.Read"), ("Module", "Sales"), ("Description", "<script>alert(1)</script>"))).StatusCode);
        var permissions = await client.GetStringAsync("/Permissions");
        Assert.Contains("&lt;script&gt;", permissions);
        Assert.DoesNotContain("<script>alert(1)</script>", permissions);
        Guid permissionId;
        Guid branchId;
        Guid adminRoleId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            permissionId = await db.Permissions.Select(x => x.Id).SingleAsync();
            branchId = await db.Branches.Select(x => x.Id).SingleAsync();
            adminRoleId = await db.Roles.Where(x => x.Name == "Admin").Select(x => x.Id).SingleAsync();
        }
        var assignmentForm = await client.GetStringAsync("/Assignments/Create");
        Assert.Contains(permissionId.ToString(), assignmentForm);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Assignments/Create", Token(assignmentForm),
            ("RoleId", roleId.ToString()), ("PermissionId", permissionId.ToString()))).StatusCode);
        Assert.Contains("Sales.Read", await client.GetStringAsync("/Assignments"));
        var revokeUrl = $"/Assignments/Delete?roleId={roleId}&permissionId={permissionId}";
        Assert.Equal(HttpStatusCode.OK, (await Post(client, revokeUrl, Token(await client.GetStringAsync(revokeUrl)))).StatusCode);
        var permissionEditUrl = "/Permissions/Edit/" + permissionId;
        Assert.Equal(HttpStatusCode.OK, (await Post(client, permissionEditUrl, Token(await client.GetStringAsync(permissionEditUrl)),
            ("Code", "Sales.Update"), ("Module", "Sales"))).StatusCode);
        var deleteUrl = "/Permissions/Delete/" + permissionId;
        Assert.Equal(HttpStatusCode.OK, (await Post(client, deleteUrl, Token(await client.GetStringAsync(deleteUrl)))).StatusCode);
        var systemEdit = "/Roles/Edit/" + adminRoleId;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(client, systemEdit, Token(await client.GetStringAsync(systemEdit)), ("Name", "Renamed"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/RecycleBin?section=ApplicationUser")).StatusCode);
        var userForm = await client.GetStringAsync("/Users/Create");
        Assert.Contains("Profile.BranchId", userForm);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Users/Create", Token(userForm),
            ("Profile.FullName", "Test user"), ("Profile.UserName", "ui-created"), ("Profile.Email", "created@example.test"),
            ("Profile.BranchId", branchId.ToString()), ("Profile.IsActive", "true"), ("Password", "Test-only!2026"))).StatusCode);
        Assert.Contains("ui-created", await client.GetStringAsync("/Users"));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/Users/Create", Token(userForm),
            ("Profile.FullName", "Invalid branch"), ("Profile.UserName", "invalid"), ("Profile.Email", "invalid@example.test"),
            ("Profile.BranchId", Guid.NewGuid().ToString()), ("Password", "Test-only!2026"))).StatusCode);
        using var reader = Client(app);
        await Login(reader, "ui-reader");
        reader.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/Roles/Create")).StatusCode);
    }

    [Fact]
    public async Task AvatarUploadAndHeaderUseValidatedContent()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        var login = await client.GetStringAsync("/Account/Login");
        Assert.DoesNotContain("id=\"sidebar\"", login);
        Assert.DoesNotContain("id=\"mobileNavigation\"", login);
        Assert.Contains("manifest.webmanifest", login);
        await Login(client, "ui-admin");
        Guid id, branch;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync("ui-admin");
            id = user!.Id; branch = user.BranchId;
        }
        var url = "/Users/Edit/" + id;
        var token = Token(await client.GetStringAsync(url));
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string,string> {
            ["__RequestVerificationToken"] = token, ["FullName"] = "Avatar Test", ["UserName"] = "ui-admin",
            ["Email"] = "ui-admin@example.test", ["BranchId"] = branch.ToString(), ["IsActive"] = "true" })
            form.Add(new StringContent(field.Value), field.Key);
        var image = await File.ReadAllBytesAsync(Path.Combine(app.Environment.ContentRootPath, "wwwroot/pwa/icon-192.png"));
        form.Add(new ByteArrayContent(image), "Avatar", "not-an-image-extension.txt");
        var saved = await client.PostAsync(url, form);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("\"kind\":\"info\"", await saved.Content.ReadAsStringAsync());
        var avatar = await client.GetAsync("/Account/Avatar");
        Assert.Equal("image/png", avatar.Content.Headers.ContentType?.MediaType);
        Assert.True(avatar.Headers.CacheControl?.NoStore);
        Assert.Contains("Avatar Test", await client.GetStringAsync("/"));
        using var invalid = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string,string> {
            ["__RequestVerificationToken"] = token, ["FullName"] = "Avatar Test", ["UserName"] = "ui-admin",
            ["Email"] = "ui-admin@example.test", ["BranchId"] = branch.ToString(), ["IsActive"] = "true" })
            invalid.Add(new StringContent(field.Value), field.Key);
        invalid.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("<script>bad</script>")), "Avatar", "photo.png");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(url, invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/manifest.webmanifest")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/service-worker.js")).StatusCode);
    }

    [Fact]
    public async Task TemporaryAdministratorStopsWhenPrimaryAdministratorExists()
    {
        await using var app = await StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var primary = (await users.FindByNameAsync("ui-admin"))!;
        Assert.True((await users.RemoveFromRoleAsync(primary, "Admin")).Succeeded);
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        config["DevelopmentAccess:Enabled"] = "true";
        config["DevelopmentAccess:UserName"] = "temporary-test";
        config["DevelopmentAccess:Password"] = "Test-only!2026";
        var bootstrap = scope.ServiceProvider.GetRequiredService<NEXT.Infrastructure.Identity.TemporaryAdministratorBootstrapper>();
        await bootstrap.InitializeAsync();
        var temporary = (await users.FindByNameAsync("temporary-test"))!;
        var policy = scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.ILoginAccessPolicy>();
        Assert.True(await policy.CanSignInAsync(temporary.Id));
        config["DevelopmentAccess:Enabled"] = "false";
        Assert.False(await policy.CanSignInAsync(temporary.Id));
        config["DevelopmentAccess:Enabled"] = "true";
        Assert.True((await users.AddToRoleAsync(primary, "Admin")).Succeeded);
        Assert.False(await policy.CanSignInAsync(temporary.Id));
        Assert.True(await policy.CanSignInAsync(primary.Id));
    }
}
