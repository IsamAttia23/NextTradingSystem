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
    public static async Task<WebApplication> StartAsync(string? contentRoot = null, string? postgresConnection = null)
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
            ["ConnectionStrings:NEXTDatabase"] = postgresConnection ?? "Host=localhost;Database=unused"
        });
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        if (postgresConnection is null)
        {
        builder.Services.RemoveAll<DbContextOptions<NEXTDbContext>>();
        builder.Services.RemoveAll<IDbContextOptionsConfiguration<NEXTDbContext>>();
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<NEXTDbContext>(options => options.UseInMemoryDatabase(database));
        }
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

    internal static HttpClient Client(WebApplication app) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false, CookieContainer = new CookieContainer()
    }) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };

    internal static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    internal static Task<HttpResponseMessage> Post(HttpClient client, string url, string token, params (string Key, string Value)[] fields)
    {
        // Existing CRUD scenarios supply a reason; dedicated validation tests explicitly post an empty reason.
        if ((url.Contains("/Delete", StringComparison.Ordinal) || url.Contains("/Purge", StringComparison.Ordinal)) && !fields.Any(x => x.Key == "Reason"))
            fields = fields.Append(("Reason", "Test deletion reason")).ToArray();
        return client.PostAsync(url, new FormUrlEncodedContent(fields.Append(("__RequestVerificationToken", token))
            .Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2))));
    }

    internal static async Task Login(HttpClient client, string name)
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
        Assert.Contains("BranchId", userForm);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Users/Create", Token(userForm),
            ("FullName", "Test user"), ("UserName", "ui-created"), ("Email", "created@example.test"),
            ("BranchId", branchId.ToString()), ("IsActive", "true"), ("Password", "Test-only!2026"))).StatusCode);
        Assert.Contains("ui-created", await client.GetStringAsync("/Users"));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/Users/Create", Token(userForm),
            ("FullName", "Invalid branch"), ("UserName", "invalid"), ("Email", "invalid@example.test"),
            ("BranchId", Guid.NewGuid().ToString()), ("Password", "Test-only!2026"))).StatusCode);
        using var reader = Client(app);
        await Login(reader, "ui-reader");
        reader.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/Roles/Create")).StatusCode);
    }

    [Fact]
    public async Task CatalogScreensAndSharedDtoFormsSupportCrudAndValidation()
    {
        await using var app = await StartAsync();
        using var client = Client(app);
        await Login(client, "ui-admin");
        foreach (var route in new[] { "Customers", "CustomersContact", "Tags", "Suppliers", "Categories", "Branches", "Products", "Units", "Warehouses", "Inventory", "Inventory/Movements", "Inventory/Batches" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/" + route)).StatusCode);
        foreach (var route in new[] { "Customers", "CustomersContact", "Tags", "Suppliers", "Categories", "Branches", "Products", "Units", "Warehouses" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/" + route + "/Create")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Inventory/Receive")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Inventory/Transfer")).StatusCode);
        var tagForm = await client.GetStringAsync("/Tags/Create");
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Tags/Create", Token(tagForm), ("Name", "Retail"), ("Color", "#102030"))).StatusCode);
        Guid tagId;
        await using (var scope = app.Services.CreateAsyncScope())
            tagId = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Tags.Select(x => x.Id).SingleAsync();
        var customerForm = await client.GetStringAsync("/Customers/Create");
        Assert.Contains(tagId.ToString(), customerForm);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/Customers/Create", Token(customerForm), ("Name", "Invalid"), ("TagId", Guid.Empty.ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/Customers/Create", Token(customerForm), ("Name", "Retail customer"), ("CreditLimit", "12.25"), ("TagId", tagId.ToString()), ("IsActive", "true"))).StatusCode);
        Assert.Contains("Retail customer", await client.GetStringAsync("/Customers"));
        Guid customerId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var customer = await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Customers.SingleAsync();
            customerId = customer.Id; Assert.Equal(12.25m, customer.CreditLimit);
        }
        var deleteTag = "/Tags/Delete/" + tagId;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post(client, deleteTag, Token(await client.GetStringAsync(deleteTag)))).StatusCode);
        var edit = "/Customers/Edit/" + customerId;
        Assert.Equal(HttpStatusCode.OK, (await Post(client, edit, Token(await client.GetStringAsync(edit)), ("Name", "Updated customer"), ("TagId", tagId.ToString()))).StatusCode);
        var delete = "/Customers/Delete/" + customerId;
        Assert.Equal(HttpStatusCode.OK, (await Post(client, delete, Token(await client.GetStringAsync(delete)))).StatusCode);
        Assert.DoesNotContain("Updated customer", await client.GetStringAsync("/Customers"));
        // A generic controller's write actions still enforce antiforgery and administration authorization.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Tags/Create", new FormUrlEncodedContent(new Dictionary<string,string> { ["Name"] = "No token" }))).StatusCode);
        using var reader = Client(app); await Login(reader, "ui-reader");
        reader.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/Inventory/Receive")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/Customers/Create")).StatusCode);
    }

    [Fact]
    public async Task SharedFiltersBadgesPaginationAndDetailsWorkAcrossCatalogEntities()
    {
        await using var app = await StartAsync(); using var client = Client(app);
        await Login(client, "ui-admin");
        Guid targetId, tagId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var tag = new Tag { Name = "Gold partner", Color = "#ffcc00" }; var other = new Tag { Name = "Other", Color = "#102030" };
            db.AddRange(tag, other); await db.SaveChangesAsync(); tagId = tag.Id;
            var target = new Customer { Name = "Alpha customer", TagId = tag.Id, Phone = "111", IsActive = true };
            db.Add(target);
            db.AddRange(Enumerable.Range(1, 24).Select(i => new Customer { Name = "Partner " + i.ToString("D2"), TagId = tag.Id, IsActive = true }));
            db.Add(new Customer { Name = "Inactive sentinel", TagId = other.Id, IsActive = false });
            await db.SaveChangesAsync(); targetId = target.Id;
            db.CustomerContacts.Add(new CustomerContact { CustomerId = target.Id, Name = "Linked contact" });
            db.Payments.Add(new Payment { CustomerId = target.Id, Amount = 99, PaymentNumber = "PAY-CUSTOMER" });
            await db.SaveChangesAsync();
        }
        var url = $"/Customers?TagId={tagId}&IsActive=true&PageSize=10&SortBy=Name&SortDirection=Ascending";
        var html = await client.GetStringAsync(url);
        Assert.Contains("Gold partner", html); Assert.Contains("background-color:#ffcc00", html);
        Assert.Contains("initial-avatar", html); Assert.DoesNotContain("Inactive sentinel", html);
        Assert.Contains("PageNumber=2", html); Assert.Contains("TagId=" + tagId, html);
        Assert.Contains("Alpha customer", html);
        var second = await client.GetStringAsync(url + "&PageNumber=2"); Assert.DoesNotContain("Alpha customer", second);
        var search = await client.GetStringAsync("/Customers?SearchTerm=Alpha&PageSize=10");
        Assert.Contains("Alpha customer", search); Assert.DoesNotContain("Partner 01", search);
        var byPhone = await client.GetStringAsync("/Customers?SearchTerm=111"); Assert.Contains("Alpha customer", byPhone);
        var inactive = await client.GetStringAsync("/Customers?IsActive=false"); Assert.Contains("Inactive sentinel", inactive); Assert.DoesNotContain("Alpha customer", inactive);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Inventory/Movements?From=2026-12-01&To=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/Customers?TagId=not-a-guid")).StatusCode);
        var details = await client.GetStringAsync("/Customers/Details/" + targetId);
        Assert.Contains("record-card", details); Assert.Contains("Gold partner", details); Assert.Contains("Linked contact", details);
        var payments = await client.GetStringAsync($"/Customers/Details/{targetId}?activityKind=Payment");
        Assert.Contains("PAY-CUSTOMER", payments); Assert.DoesNotContain("PasswordHash", details);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/Customers/Details/{targetId}?activityKind=NotASection")).StatusCode);
        var layout = await client.GetStringAsync("/Customers");
        Assert.Contains("nav-group-title", layout); Assert.True(layout.IndexOf("sidebar-profile") < layout.IndexOf("grouped-navigation"));
        Assert.DoesNotContain("sidebar-bottom", layout);
        var tagDetails = await client.GetStringAsync("/Tags/Details/" + tagId); Assert.Contains("record-card", tagDetails);
    }

    [Fact]
    public async Task PartnerPhotoUploadsAreValidatedPreservedAndRemovable()
    {
        await using var app = await StartAsync(); using var client = Client(app); await Login(client, "ui-admin");
        Guid tag;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>(); var entity = new Tag { Name = "Partners", Color = "#102030" };
            db.Tags.Add(entity); await db.SaveChangesAsync(); tag = entity.Id;
        }
        var image = await File.ReadAllBytesAsync(Path.Combine(app.Environment.ContentRootPath, "wwwroot/pwa/icon-192.png"));
        foreach (var route in new[] { "Customers", "Suppliers" })
        {
            var form = await client.GetStringAsync("/" + route + "/Create"); var token = Token(form);
            using var upload = new MultipartFormDataContent();
            upload.Add(new StringContent(token), "__RequestVerificationToken"); upload.Add(new StringContent("Photo " + route), "Name");
            upload.Add(new StringContent(tag.ToString()), "TagId"); upload.Add(new ByteArrayContent(image), "Avatar", "photo.png");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/" + route + "/Create", upload)).StatusCode);
            Guid id;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
                id = route == "Customers" ? await db.Customers.Select(x => x.Id).SingleAsync() : await db.Suppliers.Select(x => x.Id).SingleAsync();
            }
            var avatar = await client.GetAsync($"/{route}/Avatar/{id}"); Assert.Equal("image/png", avatar.Content.Headers.ContentType?.MediaType);
            Assert.Contains("/" + route + "/Avatar/" + id, await client.GetStringAsync($"/{route}/Details/{id}"));
            var editUrl = $"/{route}/Edit/{id}"; var edit = await client.GetStringAsync(editUrl);
            Assert.Equal(HttpStatusCode.OK, (await Post(client, editUrl, Token(edit), ("Name", "Preserved"), ("TagId", tag.ToString()))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/{route}/Avatar/{id}")).StatusCode);
            using var bad = new MultipartFormDataContent(); bad.Add(new StringContent(Token(edit)), "__RequestVerificationToken");
            bad.Add(new StringContent("Invalid"), "Name"); bad.Add(new StringContent(tag.ToString()), "TagId");
            bad.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("<script>bad</script>")), "Avatar", "fake.png");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(editUrl, bad)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Post(client, editUrl, Token(edit), ("Name", "First letter"), ("TagId", tag.ToString()), ("RemoveAvatar", "true"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/{route}/Avatar/{id}")).StatusCode);
            Assert.Contains("initial-avatar", await client.GetStringAsync($"/{route}/Details/{id}"));
        }
    }

    [Fact]
    public async Task DeleteConfirmationRequiresReasonAndPreservesItInRecycleBin()
    {
        await using var app = await StartAsync(); using var client = Client(app); await Login(client, "ui-admin");
        Guid id;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>(); var tag = new Tag { Name = "Deletion test", Color = "#102030" };
            db.Tags.Add(tag); await db.SaveChangesAsync(); id = tag.Id;
        }
        var url = "/Tags/Delete/" + id; var html = await client.GetStringAsync(url);
        Assert.Contains("name=\"Reason\"", html); Assert.Contains("maxlength=\"500\"", html);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, url, Token(html), ("Reason", "  "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, url, Token(html), ("Reason", new string('x',501)))).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Tags.AnyAsync(x => x.Id == id));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, url, Token(html), ("Reason", "  Duplicate record  "))).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            Assert.Equal("Duplicate record", (await db.Tags.IgnoreQueryFilters().SingleAsync(x => x.Id == id)).DeletionReason);
            Assert.Equal("Duplicate record", (await db.RecycleBinEntries.SingleAsync(x => x.EntityId == id)).DeleteReason);
            Assert.Contains("Duplicate record", (await db.AuditLogs.SingleAsync(x => x.EntityId == id && x.Action == NEXT.Domain.Enums.AuditAction.Delete)).NewValuesJson!);
            Assert.Null(scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Context.IDeletionAuditContext>().Reason);
        }
        Assert.Contains("Duplicate record", await client.GetStringAsync("/RecycleBin?section=Tag"));
        var page = await client.GetStringAsync("/Tags");
        Assert.Contains("sidebar-header", page); Assert.Contains("sidebar-scroll", page); Assert.Contains("sidebar-footer", page); Assert.Contains("app-footer", page);
        Assert.Contains("data-success-title", page);
    }

    [Fact]
    public async Task FiltersMatchEachQueryModelAndPageSizeLivesInNavigation()
    {
        await using var app = await StartAsync(); using var client = Client(app); await Login(client,"ui-admin");
        var customers = await client.GetStringAsync("/Customers");
        foreach (var field in new[]{"Type","Category","TaxNumber","CommercialRegister","MinCreditLimit","MaxCreditLimit","MinCurrentBalance","MaxCurrentBalance","MinPaymentTermsDays","MaxPaymentTermsDays","TagId","IsActive"})
            Assert.Contains("name=\""+field+"\"", customers);
        var panel = Regex.Match(customers,"<details[^>]*data-filter-panel[\\s\\S]*?</details>").Value;
        Assert.NotEmpty(panel); Assert.DoesNotContain("<details open",panel);
        Assert.DoesNotContain("<select class=\"form-select\" name=\"PageSize\"",panel);
        Assert.Contains("class=\"list-navigation\"",customers); Assert.Contains("data-page-size",customers);
        Assert.DoesNotContain("name=\"From\"",customers);
        var suppliers = await client.GetStringAsync("/Suppliers");
        Assert.Contains("name=\"IsActive\"",suppliers); Assert.DoesNotContain("name=\"MinCreditLimit\"",suppliers); Assert.DoesNotContain("name=\"TaxNumber\"",suppliers);
        var products = await client.GetStringAsync("/Products");
        foreach(var field in new[]{"Barcode","Name","Description","CategoryId","UnitOfMeasureId","MinCostPrice","MaxRetailPrice","MinWholesalePrice","MaxVipPrice","MinTaxRate","HasExpiry","IsActive"})
            Assert.Contains("name=\""+field+"\"",products);
        var movements = await client.GetStringAsync("/Inventory/Movements");
        foreach(var field in new[]{"ProductId","WarehouseId","Type","From","To"}) Assert.Contains("name=\""+field+"\"",movements);
        var font = await client.GetByteArrayAsync("/fonts/tajawal/Tajawal-Regular.ttf"); Assert.Equal(new byte[]{0,1,0,0},font.Take(4));
    }

    [Fact]
    public async Task EntitySpecificEnumsFinancialRangesAndProductRangesFilterBeforePaging()
    {
        await using var app = await StartAsync(); using var client = Client(app); await Login(client,"ui-admin");
        await using(var scope = app.Services.CreateAsyncScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<NEXTDbContext>(); var tag=new Tag{Name="Range tag",Color="#102030"};db.Add(tag);await db.SaveChangesAsync();
            db.AddRange(new Customer{Name="Matched company",TagId=tag.Id,Type=NEXT.Domain.Enums.CustomerType.Company,Category=NEXT.Domain.Enums.CustomerCategory.Wholesale,TaxNumber="TAX-1",CommercialRegister="REG-1",CreditLimit=100,CurrentBalance=30,PaymentTermsDays=15},
                new Customer{Name="Excluded individual",TagId=tag.Id,CreditLimit=10,CurrentBalance=2},
                new Product{Name="Matched product",Barcode="ABC-123",CostPrice=10,RetailPrice=25,WholesalePrice=20,VipPrice=18,TaxRate=5,MinStock=4,MaxStock=50,HasExpiry=true},
                new Product{Name="Excluded product",Barcode="XYZ",CostPrice=1,RetailPrice=2,MinStock=1});
            await db.SaveChangesAsync();
        }
        var html=await client.GetStringAsync("/Customers?Type=Company&Category=Wholesale&TaxNumber=TAX&CommercialRegister=REG&MinCreditLimit=90&MaxCreditLimit=110&MinCurrentBalance=20&MaxCurrentBalance=40&MinPaymentTermsDays=10&MaxPaymentTermsDays=20&PageSize=1");
        Assert.Contains("Matched company",html);Assert.DoesNotContain("Excluded individual",html);
        html=await client.GetStringAsync("/Products?Barcode=ABC&MinCostPrice=9&MaxCostPrice=11&MinRetailPrice=24&MaxRetailPrice=26&MinStock=3&MaxStock=60&HasExpiry=true");
        Assert.Contains("Matched product",html);Assert.DoesNotContain("Excluded product",html);
        foreach(var route in new[]{"/Customers?MinCreditLimit=20&MaxCreditLimit=10","/Customers?Type=999","/Products?MinRetailPrice=30&MaxRetailPrice=2","/Products?HasExpiry=invalid"})
            Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(route)).StatusCode);
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
