using Microsoft.Extensions.DependencyInjection;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.DTOs.Catalog;
using NEXT.Application.DTOs.Inventory;
using NEXT.Application.Exceptions;
using NEXT.Application.Services.Catalog;
using NEXT.Application.Services.Inventory;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Repositories;
using NEXT.Infrastructure.Persistence.SaveChanges;
using Xunit;
namespace NEXT.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NEXT_RUN_POSTGRES_TESTS") != "1")
            Skip = "Set NEXT_RUN_POSTGRES_TESTS=1 and provide a local PostgreSQL connection with CREATE DATABASE permission.";
    }
}

/// <summary>Runs against a new, isolated PostgreSQL database per test, never the application's database.</summary>
public sealed class PostgresInventoryTests
{
    [PostgresFact]
    public async Task ReceiptUpdatesWeightedCostAndRejectsDuplicateOperation()
    {
        await using var fixture = await Database.Create();
        var service = fixture.Service();
        var first = fixture.Receipt(10, 4);
        await service.ReceiveAsync(first);
        await service.ReceiveAsync(fixture.Receipt(5, 10));
        fixture.Db.ChangeTracker.Clear();
        var stock = await fixture.Db.Stocks.SingleAsync();
        Assert.Equal(15m, stock.Quantity);
        Assert.Equal(6m, stock.AverageCost);
        Assert.Equal(2, await fixture.Db.StockMovements.CountAsync());
        await Assert.ThrowsAsync<ConflictException>(() => service.ReceiveAsync(first));
        Assert.Equal(15m, (await fixture.Db.Stocks.SingleAsync()).Quantity);
    }

    [PostgresFact]
    public async Task TransferPreservesTotalQuantityAndUsesSourceAverageCost()
    {
        await using var f = await Database.Create();
        var service = f.Service();
        await service.ReceiveAsync(f.Receipt(10, 4));
        var destination = f.Receipt(5, 10); destination.WarehouseId = f.Target;
        await service.ReceiveAsync(destination);
        var transfer = f.Transfer(5);
        await service.TransferAsync(transfer);
        f.Db.ChangeTracker.Clear();
        var source = await f.Db.Stocks.SingleAsync(x => x.WarehouseId == f.Source);
        var target = await f.Db.Stocks.SingleAsync(x => x.WarehouseId == f.Target);
        Assert.Equal(5m, source.Quantity);
        Assert.Equal(10m, target.Quantity);
        Assert.Equal(7m, target.AverageCost);
        Assert.Equal(15m, source.Quantity + target.Quantity);
        var ledger = await f.Db.StockMovements.Where(x => x.ReferenceId == transfer.OperationId).ToListAsync();
        Assert.Equal(2, ledger.Count);
        Assert.Contains(ledger, x => x.Type == StockMovementType.TransferOut && x.WarehouseId == f.Source && x.UnitCost == 4);
        Assert.Contains(ledger, x => x.Type == StockMovementType.TransferIn && x.WarehouseId == f.Target && x.UnitCost == 4);
        await Assert.ThrowsAsync<ConflictException>(() => service.TransferAsync(transfer));
    }

    [PostgresFact]
    public async Task TransferRejectsReservedInsufficientSameWarehouseAndInactiveTargets()
    {
        await using var f = await Database.Create();
        var service = f.Service();
        await service.ReceiveAsync(f.Receipt(10, 4));
        var stock = await f.Db.Stocks.SingleAsync(); stock.ReservedQuantity = 8;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AppValidationException>(() => service.TransferAsync(f.Transfer(3)));
        var same = f.Transfer(1); same.DestinationWarehouseId = f.Source;
        await Assert.ThrowsAsync<AppValidationException>(() => service.TransferAsync(same));
        var target = await f.Db.Warehouses.SingleAsync(x => x.Id == f.Target); target.IsActive = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AppValidationException>(() => service.TransferAsync(f.Transfer(1)));
        Assert.Single(await f.Db.StockMovements.ToListAsync());
        Assert.Equal(10m, (await f.Db.Stocks.SingleAsync()).Quantity);
    }

    [PostgresFact]
    public async Task ExpiryBatchesMoveWithBalancesAndMismatchedExpiryRollsBack()
    {
        await using var f = await Database.Create(expiry: true);
        var service = f.Service();
        var receipt = f.Receipt(10, 4); receipt.BatchNumber = "LOT-1"; receipt.ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await service.ReceiveAsync(receipt);
        var transfer = f.Transfer(4); transfer.BatchNumber = "LOT-1";
        await service.TransferAsync(transfer);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(6m, (await f.Db.Batches.SingleAsync(x => x.WarehouseId == f.Source)).Quantity);
        Assert.Equal(4m, (await f.Db.Batches.SingleAsync(x => x.WarehouseId == f.Target)).Quantity);
        var mismatch = f.Receipt(1, 20); mismatch.BatchNumber = "LOT-1"; mismatch.ExpiryDate = receipt.ExpiryDate.Value.AddDays(1);
        await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(mismatch));
        Assert.Equal(10m, await f.Db.Stocks.SumAsync(x => x.Quantity));
        Assert.Equal(10m, await f.Db.Batches.SumAsync(x => x.Quantity));
        Assert.Equal(3, await f.Db.StockMovements.CountAsync());
    }

    [PostgresFact]
    public async Task LedgerFailureRollsBackBothWarehousesAndBatches()
    {
        await using var f = await Database.Create(expiry: true);
        var service = f.Service();
        var receipt = f.Receipt(10, 4); receipt.BatchNumber = "LOT-1"; receipt.ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await service.ReceiveAsync(receipt);
        await f.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_transfer() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW."ReferenceType" = 'Transfer' THEN RAISE EXCEPTION 'Test ledger failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_transfer BEFORE INSERT ON "StockMovements" FOR EACH ROW EXECUTE FUNCTION reject_transfer();
            """);
        var transfer = f.Transfer(4); transfer.BatchNumber = "LOT-1";
        await Assert.ThrowsAsync<DbUpdateException>(() => service.TransferAsync(transfer));
        Assert.Equal(10m, (await f.Db.Stocks.SingleAsync()).Quantity);
        Assert.Equal(10m, (await f.Db.Batches.SingleAsync()).Quantity);
        Assert.Single(await f.Db.StockMovements.ToListAsync());
    }

    [PostgresFact]
    public async Task ConcurrentTransfersCannotOversellOrCreateHalfALedger()
    {
        await using var f = await Database.Create();
        await f.Service().ReceiveAsync(f.Receipt(10, 4));
        await using var second = f.NewContext();
        var tasks = new[] { Attempt(f.Service(), f.Transfer(7)), Attempt(f.Service(second), f.Transfer(7)) };
        var outcomes = await Task.WhenAll(tasks);
        Assert.Single(outcomes, x => x);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(10m, await f.Db.Stocks.SumAsync(x => x.Quantity));
        Assert.All(await f.Db.Stocks.ToListAsync(), x => Assert.True(x.Quantity >= 0));
        Assert.Equal(3, await f.Db.StockMovements.CountAsync());
        static async Task<bool> Attempt(InventoryService service, TransferStockDto input)
        {
            try { await service.TransferAsync(input); return true; }
            catch (ConflictException) { return false; }
            catch (AppValidationException) { return false; }
        }
    }

    [PostgresFact]
    public async Task InvalidQuantityCostAndMissingBatchesNeverWriteStock()
    {
        await using var f = await Database.Create(expiry: true);
        var service = f.Service();
        foreach (var quantity in new[] { 0m, -1m, 1.0001m })
            await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(f.Receipt(quantity, 4)));
        await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(f.Receipt(1, -1)));
        await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(f.Receipt(1, 1.001m)));
        await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(f.Receipt(1, 4)));
        var expired = f.Receipt(1, 4); expired.BatchNumber = "OLD"; expired.ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(expired));
        Assert.Empty(await f.Db.Stocks.ToListAsync());
        Assert.Empty(await f.Db.StockMovements.ToListAsync());
    }

    [PostgresFact]
    public async Task CatalogPersistsDtosValidatesReferencesAndProtectsBalancesAndHierarchy()
    {
        await using var f = await Database.Create();
        var tag = new Tag { Name = "Retail", Color = "#ff0000" }; f.Db.Tags.Add(tag); await f.Db.SaveChangesAsync();
        var mapper = new Mapper(new TypeAdapterConfig());
        var service = new CatalogService<Customer, CustomerDto, CustomerInputDto>(new KeyRepository<Customer, Guid>(f.Db), mapper,
            new PaginationServices(), new UnitOfWork(f.Db), new DataAnnotationsEntityValidator<CustomerInputDto>(), [new CustomerRules(new TRepository<Tag>(f.Db))]);
        await Assert.ThrowsAsync<AppValidationException>(() => service.CreateAsync(new CustomerInputDto { Name = "No tag" }));
        var customer = await service.CreateAsync(new CustomerInputDto { Name = "Customer", TagId = tag.Id });
        var entity = await f.Db.Customers.SingleAsync(); entity.CurrentBalance = 99;
        await f.Db.SaveChangesAsync();
        await service.UpdateAsync(customer.Id, new CustomerInputDto { Name = "Updated", TagId = tag.Id });
        Assert.Equal(99, (await service.GetAsync(customer.Id)).CurrentBalance);
        Assert.Equal(1, (await service.ListAsync(new())).TotalCount);
        var parent = new Category { Name = "Parent" }; f.Db.Categories.Add(parent); await f.Db.SaveChangesAsync();
        var child = new Category { Name = "Child", ParentId = parent.Id }; f.Db.Categories.Add(child); await f.Db.SaveChangesAsync();
        var rules = new CategoryRules(new KeyRepository<Category, Guid>(f.Db));
        await Assert.ThrowsAsync<AppValidationException>(() => rules.ValidateAsync(parent.Id, new CategoryInputDto { Name = "Cycle", ParentId = child.Id }, default));
        await service.DeleteAsync(customer.Id, reason: "Test deletion reason");
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(customer.Id));
    }

    [PostgresFact]
    public async Task UserDeletionProtectsCurrentAndLastAdministratorAndDisablesDeletedLogin()
    {
        await using var f = await Database.Create();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:NEXTDatabase"] = f.TestConnection }).Build();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions.AddLogging(services);
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new TestEnvironment());
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(new TestEnvironment());
        NEXT.Application.EntityServiceRegistrationExtensions.AddApplicationServices(services, config);
        NEXT.Infrastructure.DependencyInjection.AddInfrastructureServices(services, config);
        services.AddSingleton<ICurrentUserService>(new CurrentUser(null));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Role>>();
        Assert.True((await roles.CreateAsync(new Role { Name = "Admin" })).Succeeded);
        var admin = new ApplicationUser { UserName = "only-admin", Email = "admin@example.test", FullName = "Admin", IsActive = true, BranchId = f.Branch };
        var victim = new ApplicationUser { UserName = "victim", Email = "victim@example.test", FullName = "Victim", IsActive = true, BranchId = f.Branch };
        Assert.True((await users.CreateAsync(admin, "Test-only!2026")).Succeeded);
        Assert.True((await users.CreateAsync(victim, "Test-only!2026")).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => mediator.Send(new NEXT.Application.Features.ApplicationUsers.Commands.DeleteApplicationUserCommand(admin.Id)));
        var selfHandler = new NEXT.Application.Features.ApplicationUsers.Commands.DeleteApplicationUserHandler(
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.IUserQueryService>(),
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.IUserCommandService>(), new CurrentUser(victim.Id),
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.IUserDeletionPolicy>(),
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Persistence.IUnitOfWork>());
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => selfHandler.Handle(new(victim.Id), default));
        Assert.True((await mediator.Send(new NEXT.Application.Features.ApplicationUsers.Commands.DeleteApplicationUserCommand(victim.Id, "Inactive account"))).Succeeded);
        Assert.NotNull((await users.FindByIdAsync(victim.Id.ToString()))?.DeletedAt);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.IUserQueryService>().FindUserAsync(victim.Id.ToString()));
        Assert.False(await scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.ILoginAccessPolicy>().CanSignInAsync(victim.Id));
        f.Db.ChangeTracker.Clear();
        var deleted = await f.Db.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == victim.Id);
        Assert.NotNull(deleted.DeletedAt); Assert.False(deleted.IsActive); Assert.Equal("Inactive account", deleted.DeletionReason);
    }
    [PostgresFact]
    public async Task ArabicHttpInventoryFormsPostDecimalsDatesAndReturnConflicts()
    {
        await using var f = await Database.Create(expiry: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var form = await client.GetStringAsync("/Inventory/Receive");
        var token = WorkspaceUiTests.Token(form);
        var operation = Guid.NewGuid().ToString();
        var receipt = new[] { ("ProductId", f.Product.ToString()), ("WarehouseId", f.Source.ToString()), ("Quantity", "10.125"),
            ("UnitCost", "4.25"), ("BatchNumber", "HTTP-BATCH"), ("ExpiryDate", DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), ("OperationId", operation) };
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/Inventory/Receive", token, receipt)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, (await WorkspaceUiTests.Post(client, "/Inventory/Receive", token, receipt)).StatusCode);
        form = await client.GetStringAsync("/Inventory/Transfer");
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/Inventory/Transfer", WorkspaceUiTests.Token(form),
            ("OperationId", Guid.NewGuid().ToString()), ("ProductId", f.Product.ToString()), ("SourceWarehouseId", f.Source.ToString()),
            ("DestinationWarehouseId", f.Target.ToString()), ("Quantity", "2.125"), ("BatchNumber", "HTTP-BATCH"))).StatusCode);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(8m, (await f.Db.Stocks.SingleAsync(x => x.WarehouseId == f.Source)).Quantity);
        foreach (var route in new[] { "/Inventory", "/Inventory/Movements", "/Inventory/Batches" })
            Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync(route)).StatusCode);
        var adminId = await f.Db.Users.Where(x => x.UserName == "ui-admin").Select(x => x.Id).SingleAsync();
        var selfDelete = "/Users/Delete/" + adminId;
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, (await WorkspaceUiTests.Post(client, selfDelete, WorkspaceUiTests.Token(await client.GetStringAsync(selfDelete)))).StatusCode);
        var readerId = await f.Db.Users.Where(x => x.UserName == "ui-reader").Select(x => x.Id).SingleAsync();
        var delete = "/Users/Delete/" + readerId;
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, delete, WorkspaceUiTests.Token(await client.GetStringAsync(delete)))).StatusCode);
        Assert.DoesNotContain("ui-reader", await client.GetStringAsync("/Users"));
    }
    [PostgresFact]
    public async Task RelatedGraphFindsInvoiceReturnsPaymentsJournalsAndStockWithoutDuplicates()
    {
        await using var f = await Database.Create();
        var tag = new Tag { Name = "Tagged", Color = "#cc9900" }; f.Db.Add(tag); await f.Db.SaveChangesAsync();
        var customer = new Customer { Name = "Customer", TagId = tag.Id }; var other = new Customer { Name = "Other", TagId = tag.Id };
        var supplier = new Supplier { Name = "Supplier", TagId = tag.Id };
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "graph-user", FullName = "Graph", BranchId = f.Branch };
        f.Db.AddRange(customer, other, supplier, user); await f.Db.SaveChangesAsync();
        var sale = new SalesInvoice { CustomerId = customer.Id, InvoiceNumber = "SALE-LINKED", UserId = user.Id, WarehouseId = f.Source };
        var order = new PurchaseOrder { SupplierId = supplier.Id, Number = "ORDER-LINKED", WarehouseId = f.Source };
        f.Db.AddRange(sale, order); await f.Db.SaveChangesAsync();
        var purchase = new PurchaseInvoice { SupplierId = supplier.Id, PurchaseOrderId = order.Id, InvoiceNumber = "BUY-LINKED", WarehouseId = f.Source };
        // A return with no CustomerId must still be reached through ParentInvoiceId.
        var returned = new SalesInvoice { ParentInvoiceId = sale.Id, InvoiceNumber = "RETURN-LINKED", UserId = user.Id, WarehouseId = f.Source, InvoiceType = InvoiceType.Return };
        f.Db.AddRange(purchase, returned); await f.Db.SaveChangesAsync();
        var payment = new Payment { CustomerId = customer.Id, SalesInvoiceId = sale.Id, Amount = 50, PaymentNumber = "PAY-LINKED" };
        var foreign = new Payment { CustomerId = other.Id, Amount = 99, PaymentNumber = "UNRELATED" };
        f.Db.AddRange(payment, foreign); await f.Db.SaveChangesAsync();
        f.Db.AddRange(new JournalEntry { EntryNumber = "JOURNAL-PAYMENT", ReferenceType = JournalReferenceType.Payment, ReferenceId = payment.Id },
            new StockMovement { ProductId = f.Product, WarehouseId = f.Source, Quantity = 1, UnitCost = 10, ReferenceType = "SalesInvoice", ReferenceId = sale.Id });
        await f.Db.SaveChangesAsync();
        var reader = new RelatedRecordsReader(f.Db);
        var result = await reader.ReadAsync<Customer>(customer.Id, "Payment", 1, default);
        Assert.Single(result.Records); Assert.Contains(result.Records[0].Fields, x => x.Value == "PAY-LINKED");
        Assert.Equal(2, result.Sections.Single(x => x.Key == "SalesInvoice").Count);
        Assert.Equal(1, result.Sections.Single(x => x.Key == "JournalEntry").Count);
        Assert.Equal(1, result.Sections.Single(x => x.Key == "StockMovement").Count);
        var supplierActivity = await reader.ReadAsync<Supplier>(supplier.Id, "PurchaseInvoice", 1, default);
        Assert.Single(supplierActivity.Records);
        Assert.Equal(1, supplierActivity.Sections.Single(x => x.Key == "PurchaseOrder").Count);
        // Server-side filters/sorting execute on PostgreSQL, not on the current page in memory.
        var filtered = NEXT.Application.Services.Catalog.CatalogQueryPipeline.Apply(f.Db.Customers.AsNoTracking(),
            new NEXT.Application.QueryModels.CustomerQueryModel { SearchTerm = "CUSTOMER", TagId = tag.Id, IsActive = true });
        Assert.Equal(customer.Id, (await filtered.SingleAsync()).Id);
    }
    [PostgresFact]
    public async Task ProfileSchemaUpgradeFixesTheMissingAvatarColumnRegression()
    {
        await using var f = await Database.Create();
        await f.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Customers\" DROP COLUMN \"AvatarUrl\"; ALTER TABLE \"Suppliers\" DROP COLUMN \"AvatarUrl\";");
        var service = new CatalogService<Customer, CustomerDto, CustomerInputDto>(new KeyRepository<Customer, Guid>(f.Db), new Mapper(new TypeAdapterConfig()),
            new PaginationServices(), new UnitOfWork(f.Db), new DataAnnotationsEntityValidator<CustomerInputDto>(), []);
        var error = await Assert.ThrowsAsync<PostgresException>(() => service.ListAsync(new()));
        Assert.Equal("42703", error.SqlState);
        var sql = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/sql/20261028_partner_profiles.sql")));
        await f.Db.Database.ExecuteSqlRawAsync(sql);
        await f.Db.Database.ExecuteSqlRawAsync(sql); // The upgrade is safe to repeat.
        Assert.Equal(0, (await service.ListAsync(new())).TotalCount);
        Assert.Equal(0, await f.Db.Suppliers.CountAsync());
    }
    [PostgresFact]
    public async Task PermanentDeletionAndPermissionRevocationPersistReasonsInAudit()
    {
        await using var f = await Database.Create();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app); await WorkspaceUiTests.Login(client, "ui-admin");
        var form = await client.GetStringAsync("/Roles/Create");
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/Roles/Create", WorkspaceUiTests.Token(form), ("Name", "Audit role"))).StatusCode);
        form = await client.GetStringAsync("/Permissions/Create");
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/Permissions/Create", WorkspaceUiTests.Token(form), ("Code", "Audit.Read"), ("Module", "Audit"))).StatusCode);
        var roleId = await f.Db.Roles.Where(x => x.Name == "Audit role").Select(x => x.Id).SingleAsync();
        var permissionId = await f.Db.Permissions.Select(x => x.Id).SingleAsync();
        form = await client.GetStringAsync("/Assignments/Create");
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/Assignments/Create", WorkspaceUiTests.Token(form), ("RoleId", roleId.ToString()), ("PermissionId", permissionId.ToString()))).StatusCode);
        var url = $"/Assignments/Delete?roleId={roleId}&permissionId={permissionId}";
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, url, WorkspaceUiTests.Token(await client.GetStringAsync(url)), ("Reason", "Access no longer needed"))).StatusCode);
        Assert.Contains("Access no longer needed", (await f.Db.AuditLogs.SingleAsync(x => x.EntityType == "RolePermission" && x.Action == AuditAction.Purge)).NewValuesJson!);
        foreach (var entry in new[] { (Route: "Permissions", Id: permissionId), (Route: "Roles", Id: roleId) })
        {
            url = $"/{entry.Route}/Delete/{entry.Id}";
            var token = WorkspaceUiTests.Token(await client.GetStringAsync(url));
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await WorkspaceUiTests.Post(client, url, token, ("Reason", ""))).StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, url, token, ("Reason", "Replaced by a new policy"))).StatusCode);
            Assert.Contains("Replaced by a new policy", (await f.Db.AuditLogs.SingleAsync(x => x.EntityId == entry.Id && x.Action == AuditAction.Purge)).NewValuesJson!);
        }
    }
    [PostgresFact]
    public async Task QueryModelRangesAndAvailableStockArithmeticTranslateToPostgres()
    {
        await using var f=await Database.Create();
        var product=await f.Db.Products.SingleAsync(); product.CostPrice=10;product.RetailPrice=25;product.MinStock=5;product.MaxStock=100;product.HasExpiry=true;
        f.Db.Stocks.Add(new Stock{ProductId=f.Product,WarehouseId=f.Source,Quantity=20,ReservedQuantity=6,AverageCost=10});await f.Db.SaveChangesAsync();
        var products=NEXT.Application.Services.Catalog.CatalogQueryPipeline.Apply(f.Db.Products.AsNoTracking(),new NEXT.Application.QueryModels.ProductQueryModel{MinCostPrice=9,MaxRetailPrice=30,MinStock=4,MaxStock=101,HasExpiry=true});
        Assert.Single(await products.ToListAsync());
        var available=NEXT.Application.Services.Catalog.CatalogQueryPipeline.Apply(f.Db.Stocks.AsNoTracking(),new NEXT.Application.QueryModels.StockQueryModel{MinAvailableQuantity=14,MaxAvailableQuantity=14,SearchTerm="TEST PRODUCT"});
        Assert.Single(await available.ToListAsync());
        var excluded=NEXT.Application.Services.Catalog.CatalogQueryPipeline.Apply(f.Db.Stocks.AsNoTracking(),new NEXT.Application.QueryModels.StockQueryModel{MinAvailableQuantity=15});
        Assert.Empty(await excluded.ToListAsync());
    }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUserService;
    private sealed class TestEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "NEXT.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly string name = "next_tests_" + Guid.NewGuid().ToString("N");
        private readonly string adminConnection;
        private string Connection => new NpgsqlConnectionStringBuilder(adminConnection) { Database = name }.ConnectionString;
        public string TestConnection => Connection;
        public Guid Branch { get; private set; }
        public NEXTDbContext Db { get; private set; } = null!;
        public Guid Product { get; private set; }
        public Guid Source { get; private set; }
        public Guid Target { get; private set; }
        private Database(string connection) => adminConnection = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", Pooling = false }.ConnectionString;
        public static async Task<Database> Create(bool expiry = false)
        {
            var configuration = new ConfigurationBuilder().AddUserSecrets("57f7acf9-66bb-4e86-a018-95f71c8d9629").AddEnvironmentVariables().Build();
            var connection = Environment.GetEnvironmentVariable("NEXT_TEST_POSTGRES") ?? configuration.GetConnectionString("NEXTDatabase")
                ?? throw new InvalidOperationException("Configure NEXT_TEST_POSTGRES for the local test server.");
            var database = new Database(connection);
            await using var admin = new NpgsqlConnection(database.adminConnection);
            await admin.OpenAsync();
            await new NpgsqlCommand("CREATE DATABASE " + new NpgsqlCommandBuilder().QuoteIdentifier(database.name), admin).ExecuteNonQueryAsync();
            try
            {
                database.Db = database.NewContext();
                await database.Db.Database.EnsureCreatedAsync();
                var product = new Product { Name = "Test product", HasExpiry = expiry };
                var source = new Warehouse { Name = "Source", Code = "S" };
                var target = new Warehouse { Name = "Target", Code = "T" };
                var branch = new Branch { Name = "Test branch" };
                database.Db.AddRange(product, source, target, branch);
                await database.Db.SaveChangesAsync();
                database.Branch = branch.Id;
                database.Product = product.Id; database.Source = source.Id; database.Target = target.Id;
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }
        public NEXTDbContext NewContext()
        {
            var context = new TestContext(); var serializer = new AuditValueSerializer();
            return new NEXTDbContext(new DbContextOptionsBuilder<NEXTDbContext>().UseNpgsql(Connection).Options,
                new TrackedChangesProcessor(context, serializer, new SoftDeleteHandler(serializer), new AuditFieldsHandler()),
                new AuditEntryFactory(context, context, serializer));
        }
        public InventoryService Service(NEXTDbContext? context = null)
        {
            var db = context ?? Db;
            return new(new KeyRepository<Product, Guid>(db), new KeyRepository<Warehouse, Guid>(db), new TRepository<Stock>(db),
                new TRepository<StockMovement>(db), new TRepository<ProductBatch>(db), new UnitOfWork(db),
                new DataAnnotationsEntityValidator<ReceiveStockDto>(), new DataAnnotationsEntityValidator<TransferStockDto>());
        }
        public ReceiveStockDto Receipt(decimal quantity, decimal cost) => new() { ProductId = Product, WarehouseId = Source, Quantity = quantity, UnitCost = cost };
        public TransferStockDto Transfer(decimal quantity) => new() { ProductId = Product, SourceWarehouseId = Source, DestinationWarehouseId = Target, Quantity = quantity };
        public async ValueTask DisposeAsync()
        {
            if (Db is not null) await Db.DisposeAsync();
            await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
            // Only this fixture's generated database name is ever dropped.
            if (!name.StartsWith("next_tests_", StringComparison.Ordinal) || name.Length != 43) throw new InvalidOperationException("Invalid test database name.");
            await new NpgsqlCommand("DROP DATABASE IF EXISTS " + new NpgsqlCommandBuilder().QuoteIdentifier(name) + " WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
        private sealed class TestContext : ICurrentUserService, IRequestMetadataService
        {
            public Guid? UserId => null;
            public string? IpAddress => null;
            public string? UserAgent => null;
        }
    }
}
