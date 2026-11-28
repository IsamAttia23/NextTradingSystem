using NEXT.Application.DTOs.Stores;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Stores;
using Npgsql;
using Xunit;

namespace NEXT.Tests;

public sealed class StoreIsolationTests
{
    [PostgresFact]
    public async Task IndependentStoresSeparateAuthenticationCustomersInvoicesAndDebtBooks()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create(migrate: true);
        var operations = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection,
            configureConfiguration: config =>
            {
                config["Stores:Enabled"] = "true";
                config["Inventory:AllowInvoiceOverselling"] = "true";
                config["Email:Smtp:Host"] = "";
                config["Email:Resend:ApiKey"] = "";
                config["Email:Gmail:ClientId"] = "";
            });
        try
        {
            using var root = WorkspaceUiTests.Client(app);
            await WorkspaceUiTests.Login(root, "ui-admin");
            var sharedCustomer = Guid.NewGuid();
            var sharedProduct = Guid.NewGuid();
            var sharedInvoiceOperation = Guid.NewGuid();
            await using (var rootScope = app.Services.CreateAsyncScope())
            {
                var rootDb = rootScope.ServiceProvider.GetRequiredService<NEXTDbContext>();
                rootDb.Customers.Add(new Customer { Name = "ROOT PRIVATE CUSTOMER" });
                await rootDb.SaveChangesAsync();
            }
            var registrations = new List<StoreRegistration>();
            var cookies = new List<string>();
            var passwords = new[] { "Trial-Alpha!Owner-2026", "Trial-Beta!Owner-2026" };
            var slugs = new[] { "alpha-" + operations[0].ToString("N")[..8], "beta-" + operations[1].ToString("N")[..8] };
            for (var index = 0; index < 2; index++)
            {
                var form = await root.GetStringAsync("/Stores/Create");
                using var created = await WorkspaceUiTests.Post(root, "/Stores/Create", WorkspaceUiTests.Token(form),
                    ("OperationId", operations[index].ToString()), ("Name", index == 0 ? "Alpha store" : "Beta store"),
                    ("Slug", slugs[index]), ("OwnerName", "Trial owner"), ("OwnerUserName", "merchant"),
                    ("OwnerEmail", "owner-" + index + "@example.test"), ("OwnerPassword", passwords[index]));
                Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
                await using var rootScope = app.Services.CreateAsyncScope();
                var registered = await rootScope.ServiceProvider.GetRequiredService<StoreCatalog>().FindAsync(operations[index], default);
                Assert.NotNull(registered);
                Assert.Equal(StoreState.Active, registered!.State);
                registrations.Add(registered);
                // Retrying the same creation must neither duplicate the store nor reset its owner's password.
                using (var retry = await WorkspaceUiTests.Post(root, "/Stores/Create", WorkspaceUiTests.Token(form),
                    ("OperationId", operations[index].ToString()), ("Name", registered.Name), ("Slug", slugs[index]),
                    ("OwnerName", "Trial owner"), ("OwnerUserName", "merchant"),
                    ("OwnerEmail", "owner-" + index + "@example.test"), ("OwnerPassword", "Do-Not-Reset!2026")))
                    Assert.True(retry.IsSuccessStatusCode, await retry.Content.ReadAsStringAsync());
                await using var merchantScope = app.Services.GetRequiredService<StoreScopeFactory>().Create(registered);
                var db = merchantScope.ServiceProvider.GetRequiredService<NEXTDbContext>();
                Assert.Equal(1, await db.Users.CountAsync());
                Assert.Empty(await db.Customers.ToListAsync());
                db.Customers.Add(new Customer { Id = sharedCustomer, Name = index == 0 ? "Alpha private customer" : "Beta private customer", Phone = "+249123456789" });
                db.Products.Add(new Product { Id = sharedProduct, Name = index == 0 ? "Alpha item" : "Beta item", CostPrice = index == 0 ? 10 : 20 });
                await db.SaveChangesAsync();
                using var merchant = WorkspaceUiTests.Client(app);
                var prefix = "/s/" + slugs[index];
                var login = await merchant.GetStringAsync(prefix + "/Account/Login");
                Assert.Contains(registered.Name, login);
                using var signedIn = await WorkspaceUiTests.Post(merchant, prefix + "/Account/Login", WorkspaceUiTests.Token(login),
                    ("UserName", "merchant"), ("Password", passwords[index]), ("ReturnUrl", prefix + "/Customers"));
                Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
                Assert.Contains(prefix + "/Customers", signedIn.Headers.Location!.ToString());
                var authCookie = signedIn.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Identity.Application.", StringComparison.Ordinal));
                Assert.Contains(registered.Id.ToString("N"), authCookie);
                Assert.Contains("path=" + prefix, authCookie, StringComparison.OrdinalIgnoreCase);
                cookies.Add(authCookie.Split(';')[0]);
                var currencyForm = await merchant.GetStringAsync(prefix + "/Currency/Settings");
                using (var rate = await WorkspaceUiTests.Post(merchant, prefix + "/Currency", WorkspaceUiTests.Token(currencyForm),
                    ("UsdToSdg", index == 0 ? "1" : "2")))
                    Assert.True(rate.IsSuccessStatusCode, await rate.Content.ReadAsStringAsync());
                var customers = WebUtility.HtmlDecode(await merchant.GetStringAsync(prefix + "/Customers"));
                Assert.Contains(index == 0 ? "Alpha private customer" : "Beta private customer", customers);
                Assert.DoesNotContain(index == 0 ? "Beta private customer" : "Alpha private customer", customers);
                Assert.DoesNotContain("ROOT PRIVATE CUSTOMER", customers);
                using (var missing = new HttpRequestMessage(HttpMethod.Get, prefix + "/InvoiceDocuments/Pdf/" + Guid.NewGuid() + "?culture=en"))
                {
                    missing.Headers.Accept.ParseAdd("text/html");
                    using var error = await merchant.SendAsync(missing);
                    Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
                    var html = await error.Content.ReadAsStringAsync();
                    Assert.Contains(error.Headers.GetValues("X-Request-Id").Single(), html);
                    Assert.Contains(prefix + "/Account/Profile", html);
                    Assert.Contains("data-app-base=\"" + prefix + "\"", html);
                }
                using (var forbidden = await merchant.GetAsync(prefix + "/Stores"))
                {
                    Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
                    Assert.Contains(prefix + "/Account/Denied", forbidden.Headers.Location!.ToString());
                }
                var createInvoice = await merchant.GetStringAsync(prefix + "/Invoices/Create");
                using (var made = await WorkspaceUiTests.Post(merchant, prefix + "/Invoices/Create", WorkspaceUiTests.Token(createInvoice),
                    ("OperationId", sharedInvoiceOperation.ToString()), ("CustomerId", sharedCustomer.ToString()),
                    ("CustomerName", index == 0 ? "Alpha private customer" : "Beta private customer"),
                    ("Lines[0].ProductId", sharedProduct.ToString()), ("Lines[0].Quantity", "1"),
                    ("Lines[0].QuotedUnitPrice", index == 0 ? "10" : "20")))
                    Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync());
                var invoice = await db.Invoices.AsNoTracking().SingleAsync();
                var confirm = await merchant.GetStringAsync(prefix + "/Invoices/Confirm/" + invoice.Id);
                using (var confirmed = await WorkspaceUiTests.Post(merchant, prefix + "/Invoices/Confirm/" + invoice.Id, WorkspaceUiTests.Token(confirm),
                    ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)), ("Amount", "0")))
                    Assert.True(confirmed.IsSuccessStatusCode, await confirmed.Content.ReadAsStringAsync());
                invoice = await db.Invoices.AsNoTracking().SingleAsync();
                var fulfill = await merchant.GetStringAsync(prefix + "/Invoices/Fulfill/" + invoice.Id);
                using (var delivered = await WorkspaceUiTests.Post(merchant, prefix + "/Invoices/Fulfill/" + invoice.Id, WorkspaceUiTests.Token(fulfill),
                    ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
                    ("TargetStatus", "Delivered"), ("CustomerPhone", "+249123456789")))
                    Assert.True(delivered.IsSuccessStatusCode, await delivered.Content.ReadAsStringAsync());
                var printed = WebUtility.HtmlDecode(await merchant.GetStringAsync(prefix + "/InvoiceDocuments/Print/" + invoice.Id));
                Assert.Contains(registered.Name, printed);
                Assert.Contains(index == 0 ? "Alpha item" : "Beta item", printed);
                Assert.DoesNotContain(index == 0 ? "Beta item" : "Alpha item", printed);
                var debts = WebUtility.HtmlDecode(await merchant.GetStringAsync(prefix + "/DebtBook"));
                Assert.Contains(index == 0 ? "Alpha private customer" : "Beta private customer", debts);
                Assert.DoesNotContain(index == 0 ? "Beta private customer" : "Alpha private customer", debts);
                Assert.Equal(index == 0 ? 10 : 20, (await db.Invoices.AsNoTracking().SingleAsync()).RemainingAmount);
            }
            // Even after renaming the cookie, the signed store claim must reject replay into another database.
            using var forgedClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = root.BaseAddress };
            using var forgedRequest = new HttpRequestMessage(HttpMethod.Get, "/s/" + slugs[1] + "/Customers");
            forgedRequest.Headers.Add("Cookie", cookies[0].Replace(registrations[0].Id.ToString("N"), registrations[1].Id.ToString("N")));
            using (var replay = await forgedClient.SendAsync(forgedRequest))
            {
                Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
                Assert.Contains("/s/" + slugs[1] + "/Account/Login", replay.Headers.Location!.ToString());
            }
            var platform = await root.GetStringAsync("/Stores");
            Assert.DoesNotContain(passwords[0], platform);
            Assert.DoesNotContain(passwords[1], platform);
            using (var disabled = await WorkspaceUiTests.Post(root, "/Stores/Activate", WorkspaceUiTests.Token(platform),
                ("Id", registrations[0].Id.ToString()), ("Version", registrations[0].Version.ToString()), ("Active", "false")))
                Assert.Equal(HttpStatusCode.Redirect, disabled.StatusCode);
            using var closed = await root.GetAsync("/s/" + slugs[0] + "/Customers");
            Assert.Equal(HttpStatusCode.NotFound, closed.StatusCode);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var catalog = scope.ServiceProvider.GetRequiredService<StoreCatalog>();
                Assert.Equal(2, (await catalog.ListAsync(default)).Count);
                var disabledStore = await catalog.FindAsync(registrations[0].Id, default);
                Assert.Equal(StoreState.Disabled, disabledStore!.State);
                using var enabled = await WorkspaceUiTests.Post(root, "/Stores/Activate", WorkspaceUiTests.Token(platform),
                    ("Id", disabledStore.Id.ToString()), ("Version", disabledStore.Version.ToString()), ("Active", "true"));
                Assert.Equal(HttpStatusCode.Redirect, enabled.StatusCode);
                var active = await catalog.FindAsync(disabledStore.Id, default);
                await using var merchantScope = app.Services.GetRequiredService<StoreScopeFactory>().Create(active!);
                var db = merchantScope.ServiceProvider.GetRequiredService<NEXTDbContext>();
                Assert.Single(await db.Customers.ToListAsync());
                Assert.Equal(10, (await db.Invoices.SingleAsync()).RemainingAmount);
            }
            using var unknown = await root.GetAsync("/s/missing-store/Customers");
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally
        {
            await app.DisposeAsync();
            var options = new NpgsqlConnectionStringBuilder(fixture.TestConnection) { Database = "postgres", Pooling = false };
            await using var admin = new NpgsqlConnection(options.ConnectionString);
            await admin.OpenAsync();
            foreach (var operation in operations)
            {
                var name = StoreServerSettings.DatabaseName(operation);
                Assert.Equal("nts_trial_" + operation.ToString("N"), name);
                var quoted = new NpgsqlCommandBuilder().QuoteIdentifier(name);
                await new NpgsqlCommand("DROP DATABASE IF EXISTS " + quoted + " WITH (FORCE)", admin).ExecuteNonQueryAsync();
            }
        }
    }
}
