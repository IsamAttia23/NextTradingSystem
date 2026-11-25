using NEXT.Infrastructure.Identity.Entities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NEXT.web.Models;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Common.QueryModels;
using NEXT.Infrastructure.Persistence.Repositories.Entities;
using NEXT.Application.Features.Customers;
using NEXT.Application.Features.Categories;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.Categories;
using Microsoft.Extensions.DependencyInjection;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NEXT.Application.Abstractions.Context;

using NEXT.Application.DTOs.Inventory;
using NEXT.Application.Exceptions;
using NEXT.Application.Features;
using NEXT.Application.Services.Inventory;
using NEXT.Application.Services.Currencies;
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
        {
            Skip = "Set NEXT_RUN_POSTGRES_TESTS=1 and provide a local PostgreSQL connection with CREATE DATABASE permission.";
        }
    }
}

/// <summary>Runs against a new, isolated PostgreSQL database per test, never the application's database.</summary>
public sealed class PostgresInventoryTests
{
    [PostgresFact]
    public async Task ReceiptUpdatesWeightedCostAndRejectsDuplicateOperation()
    {
        await using var fixture = await Database.Create(withReceiptRate: true);
        var service = fixture.Service();
        var first = fixture.Receipt(10);
        await service.ReceiveAsync(first);
        await fixture.SetProductCostAsync(10);
        await service.ReceiveAsync(fixture.Receipt(5));
        fixture.Db.ChangeTracker.Clear();
        var stock = await fixture.Db.Stocks.SingleAsync();
        Assert.Equal(15m, stock.Quantity);
        Assert.Equal(6m, stock.AverageCost);
        Assert.Equal(2, await fixture.Db.StockMovements.CountAsync());
        await Assert.ThrowsAsync<ConflictException>(() => service.ReceiveAsync(first));
        Assert.Equal(15m, (await fixture.Db.Stocks.SingleAsync()).Quantity);
    }

    [PostgresFact]
    public async Task InvalidOpeningWarehouseRollsBackProductCreation()
    {
        await using var fixture = await Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection);
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
        var warehouse = await fixture.Db.Warehouses.SingleAsync(x => x.Id == fixture.Source);
        warehouse.IsActive = false;
        await fixture.Db.SaveChangesAsync();

        var input = new NEXT.Application.DTOs.Products.ProductInputDto
        {
            Name = "Rejected opening stock",
            CostPrice = 2.50m,
            AddOpeningStock = true,
            OpeningWarehouseId = fixture.Source,
            OpeningQuantity = 15
        };
        await Assert.ThrowsAsync<AppValidationException>(() =>
            sender.Send(new CreateEntityCommand<NEXT.Application.DTOs.Products.ProductDto,
                NEXT.Application.DTOs.Products.ProductInputDto>(input)));
        Assert.False(await fixture.Db.Products.AnyAsync(x => x.Name == input.Name));
        Assert.Empty(await fixture.Db.Stocks.ToListAsync());
        Assert.Empty(await fixture.Db.StockMovements.ToListAsync());
    }

    [PostgresFact]
    public async Task TransferPreservesTotalQuantityAndUsesSourceAverageCost()
    {
        await using var f = await Database.Create(withReceiptRate: true);
        var service = f.Service();
        await service.ReceiveAsync(f.Receipt(10));
        await f.SetProductCostAsync(10);
        var destination = f.Receipt(5);
        destination.WarehouseId = f.Target;
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
        await using var f = await Database.Create(withReceiptRate: true);
        var service = f.Service();
        await service.ReceiveAsync(f.Receipt(10));
        var stock = await f.Db.Stocks.SingleAsync();
        stock.ReservedQuantity = 8;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AppValidationException>(() => service.TransferAsync(f.Transfer(3)));
        var same = f.Transfer(1);
        same.DestinationWarehouseId = f.Source;
        await Assert.ThrowsAsync<AppValidationException>(() => service.TransferAsync(same));
        var target = await f.Db.Warehouses.SingleAsync(x => x.Id == f.Target);
        target.IsActive = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<AppValidationException>(() => service.TransferAsync(f.Transfer(1)));
        Assert.Single(await f.Db.StockMovements.ToListAsync());
        Assert.Equal(10m, (await f.Db.Stocks.SingleAsync()).Quantity);
    }

    [PostgresFact]
    public async Task ExpiryBatchesMoveWithBalancesAndMismatchedExpiryRollsBack()
    {
        await using var f = await Database.Create(expiry: true, withReceiptRate: true);
        var service = f.Service();
        var receipt = f.Receipt(10);
        receipt.BatchNumber = "LOT-1";
        receipt.ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await service.ReceiveAsync(receipt);
        var transfer = f.Transfer(4);
        transfer.BatchNumber = "LOT-1";
        await service.TransferAsync(transfer);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(6m, (await f.Db.Batches.SingleAsync(x => x.WarehouseId == f.Source)).Quantity);
        Assert.Equal(4m, (await f.Db.Batches.SingleAsync(x => x.WarehouseId == f.Target)).Quantity);
        var mismatch = f.Receipt(1);
        mismatch.BatchNumber = "LOT-1";
        mismatch.ExpiryDate = receipt.ExpiryDate.Value.AddDays(1);
        await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(mismatch));
        Assert.Equal(10m, await f.Db.Stocks.SumAsync(x => x.Quantity));
        Assert.Equal(10m, await f.Db.Batches.SumAsync(x => x.Quantity));
        Assert.Equal(3, await f.Db.StockMovements.CountAsync());
    }

    [PostgresFact]
    public async Task LedgerFailureRollsBackBothWarehousesAndBatches()
    {
        await using var f = await Database.Create(expiry: true, withReceiptRate: true);
        var service = f.Service();
        var receipt = f.Receipt(10);
        receipt.BatchNumber = "LOT-1";
        receipt.ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        await service.ReceiveAsync(receipt);
        await f.Db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_transfer() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW."ReferenceType" = 'Transfer' THEN RAISE EXCEPTION 'Test ledger failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_transfer BEFORE INSERT ON "StockMovements" FOR EACH ROW EXECUTE FUNCTION reject_transfer();
            """);
        var transfer = f.Transfer(4);
        transfer.BatchNumber = "LOT-1";
        await Assert.ThrowsAsync<DbUpdateException>(() => service.TransferAsync(transfer));
        Assert.Equal(10m, (await f.Db.Stocks.SingleAsync()).Quantity);
        Assert.Equal(10m, (await f.Db.Batches.SingleAsync()).Quantity);
        Assert.Single(await f.Db.StockMovements.ToListAsync());
    }

    [PostgresFact]
    public async Task ConcurrentTransfersCannotOversellOrCreateHalfALedger()
    {
        await using var f = await Database.Create(withReceiptRate: true);
        await f.Service().ReceiveAsync(f.Receipt(10));
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
            try
            {
                await service.TransferAsync(input);
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
            catch (AppValidationException)
            {
                return false;
            }
        }
    }

    [PostgresFact]
    public async Task InvalidQuantityAndMissingBatchesNeverWriteStock()
    {
        await using var f = await Database.Create(expiry: true, withReceiptRate: true);
        var service = f.Service();
        foreach (var quantity in new[] { 0m, -1m, 1.0001m })
        {
            await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(f.Receipt(quantity)));
        }
        await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(f.Receipt(1)));
        var expired = f.Receipt(1);
        expired.BatchNumber = "OLD";
        expired.ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await Assert.ThrowsAsync<AppValidationException>(() => service.ReceiveAsync(expired));
        Assert.Empty(await f.Db.Stocks.ToListAsync());
        Assert.Empty(await f.Db.StockMovements.ToListAsync());
    }

    [PostgresFact]
    public async Task SharedCqrsPersistsDtosValidatesReferencesAndProtectsBalancesAndHierarchy()
    {
        await using var f = await Database.Create();
        var tag = new Tag
        {
            Name = "Retail",
            Color = "#ff0000"
        };
        f.Db.Tags.Add(tag);
        await f.Db.SaveChangesAsync();
        var mapper = new Mapper(new TypeAdapterConfig());
        var repository = new CustomerRepository(f.Db);
        var create = new CreateEntityCommandHandler<Customer, CustomerDto, CustomerInputDto>(repository, mapper,
            new UnitOfWork(f.Db), new DataAnnotationsEntityValidator<CustomerInputDto>(), [new CustomerRules(new TRepository<Tag>(f.Db))]);
        var update = new UpdateEntityCommandHandler<Customer, CustomerDto, CustomerInputDto>(repository, mapper,
            new UnitOfWork(f.Db), new DataAnnotationsEntityValidator<CustomerInputDto>(), [new CustomerRules(new TRepository<Tag>(f.Db))]);
        var get = new GetEntityByIdQueryHandler<Customer, CustomerDto>(repository, new QueryExecutor());
        var list = new GetEntitiesQueryHandler<Customer, CustomerDto>(repository, new PaginationServices(), new DataAnnotationsEntityValidator<PaginationQuery>(), new TypeAdapterConfig());
        var delete = new DeleteEntityCommandHandler<Customer, CustomerDto>(repository, new UnitOfWork(f.Db));
        await Assert.ThrowsAsync<AppValidationException>(() => create.Handle(new(new CustomerInputDto
        {
            Name = "Unknown tag",
            TagId = Guid.NewGuid()
        }), default));
        var customer = await create.Handle(new(new CustomerInputDto
        {
            Name = "Customer",
            TagId = tag.Id
        }), default);
        var entity = await f.Db.Customers.SingleAsync();
        entity.CurrentBalance = 99;
        await f.Db.SaveChangesAsync();
        await update.Handle(new(customer.Id, new CustomerInputDto
        {
            Name = "Updated",
            TagId = tag.Id
        }, entity.RowVersion.ToArray()), default);
        Assert.Equal(99, (await get.Handle(new(customer.Id), default))!.CurrentBalance);
        Assert.Equal(1, (await list.Handle(new(new()), default)).TotalCount);
        var parent = new Category
        {
            Name = "Parent"
        };
        f.Db.Categories.Add(parent);
        await f.Db.SaveChangesAsync();
        var child = new Category
        {
            Name = "Child",
            ParentId = parent.Id
        };
        f.Db.Categories.Add(child);
        await f.Db.SaveChangesAsync();
        var rules = new CategoryRules(new KeyRepository<Category, Guid>(f.Db));
        await Assert.ThrowsAsync<AppValidationException>(() => rules.ValidateAsync(parent.Id, new CategoryInputDto
        {
            Name = "Cycle",
            ParentId = child.Id
        }, default));
        await delete.Handle(new(customer.Id, "Test deletion reason"), default);
        await Assert.ThrowsAsync<NotFoundException>(() => get.Handle(new(customer.Id), default));
    }

    [PostgresFact]
    public async Task UserDeletionProtectsCurrentAndLastAdministratorAndDisablesDeletedLogin()
    {
        await using var f = await Database.Create();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:NEXTDatabase"] = f.TestConnection
        }).Build();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions.AddLogging(services);
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new TestEnvironment());
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(new TestEnvironment());
        NEXT.Application.ApplicationRegistrationExtensions.AddApplicationServices(services, config);
        NEXT.Infrastructure.DependencyInjection.AddInfrastructureServices(services, config);
        services.AddSingleton<ICurrentUserService>(new CurrentUser(null));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Role>>();
        Assert.True((await roles.CreateAsync(new Role
        {
            Name = "Admin"
        })).Succeeded);
        var admin = new ApplicationUser
        {
            UserName = "only-admin",
            Email = "admin@example.test",
            FullName = "Admin",
            IsActive = true,
            BranchId = f.Branch
        };
        var victim = new ApplicationUser
        {
            UserName = "victim",
            Email = "victim@example.test",
            FullName = "Victim",
            IsActive = true,
            BranchId = f.Branch
        };
        Assert.True((await users.CreateAsync(admin, "Test-only!2026")).Succeeded);
        Assert.True((await users.CreateAsync(victim, "Test-only!2026")).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, "Admin")).Succeeded);
        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => mediator.Send(new NEXT.Application.Features.ApplicationUsers.Commands.DeleteApplicationUserCommand(admin.Id, "Administrator protection test")));
        var selfHandler = new NEXT.Application.Features.ApplicationUsers.Commands.DeleteApplicationUserHandler(
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Features.ApplicationUsers.UserDtoReader>(),
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.IUserCommandService>(), new CurrentUser(victim.Id),
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.IUserDeletionPolicy>(),
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Persistence.IUnitOfWork>(),
            scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Context.IDeletionAuditContext>());
        await Assert.ThrowsAsync<UnprocessableEntityException>(() => selfHandler.Handle(new(victim.Id), default));
        Assert.True((await mediator.Send(new NEXT.Application.Features.ApplicationUsers.Commands.DeleteApplicationUserCommand(victim.Id, "Inactive account"))).Succeeded);
        Assert.NotNull((await users.FindByIdAsync(victim.Id.ToString()))?.DeletedAt);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.IUserQueryService>().FindUserAsync(victim.Id.ToString()));
        Assert.False(await scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.Identity.ILoginAccessPolicy>().CanSignInAsync(victim.Id));
        f.Db.ChangeTracker.Clear();
        var deleted = await f.Db.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == victim.Id);
        Assert.NotNull(deleted.DeletedAt);
        Assert.False(deleted.IsActive);
        Assert.Equal("Inactive account", deleted.DeletionReason);
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
            ("BatchNumber", "HTTP-BATCH"), ("ExpiryDate", DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), ("OperationId", operation) };
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/Inventory/Receive", token, receipt)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, (await WorkspaceUiTests.Post(client, "/Inventory/Receive", token, receipt)).StatusCode);
        form = await client.GetStringAsync("/Inventory/Transfer");
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, "/Inventory/Transfer", WorkspaceUiTests.Token(form),
            ("OperationId", Guid.NewGuid().ToString()), ("ProductId", f.Product.ToString()), ("SourceWarehouseId", f.Source.ToString()),
            ("DestinationWarehouseId", f.Target.ToString()), ("Quantity", "2.125"), ("BatchNumber", "HTTP-BATCH"))).StatusCode);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(8m, (await f.Db.Stocks.SingleAsync(x => x.WarehouseId == f.Source)).Quantity);
        foreach (var route in new[] { "/Inventory", "/Inventory/Movements", "/Inventory/Batches" })
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync(route)).StatusCode);
        }
        var adminId = await f.Db.Users.Where(x => x.UserName == "ui-admin").Select(x => x.Id).SingleAsync();
        var selfDelete = "/Users/Delete/" + adminId;
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, (await WorkspaceUiTests.Post(client, selfDelete, WorkspaceUiTests.Token(await client.GetStringAsync(selfDelete)))).StatusCode);
        var readerId = await f.Db.Users.Where(x => x.UserName == "ui-reader").Select(x => x.Id).SingleAsync();
        var delete = "/Users/Delete/" + readerId;
        Assert.Equal(System.Net.HttpStatusCode.OK, (await WorkspaceUiTests.Post(client, delete, WorkspaceUiTests.Token(await client.GetStringAsync(delete)))).StatusCode);
        Assert.DoesNotContain("ui-reader", await client.GetStringAsync("/Users"));
    }
    [PostgresFact]
    public async Task ConfirmationRecordsPaymentAndHistoryOnceAndPreventsOverselling()
    {
        await using var fixture = await Database.Create(migrate: true, withReceiptRate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var db = fixture.Db;
        var actor = await db.Users.SingleAsync(user => user.UserName == "ui-admin");
        var stock = new Stock { ProductId = fixture.Product, WarehouseId = fixture.Source, Quantity = 10, ReservedQuantity = 2 };
        Invoice Draft(string number, decimal quantity) => new()
        {
            InvoiceNumber = number, CustomerName = "Confirmation customer", CreatedByFullName = actor.FullName,
            UserId = actor.Id, WarehouseId = fixture.Source, Status = InvoiceStatus.Pending,
            TotalAmount = 8, SubTotal = 8, RemainingAmount = 8,
            Items = [new InvoiceItem { ProductId = fixture.Product, Quantity = quantity, UnitPrice = 4, TotalAmount = 8 }]
        };
        var first = Draft("CONFIRM-FIRST", 2);
        var left = Draft("CONFIRM-LEFT", 5);
        var right = Draft("CONFIRM-RIGHT", 5);
        db.AddRange(stock, first, left, right);
        await db.SaveChangesAsync();
        var form = await client.GetStringAsync($"/Invoices/Confirm/{first.Id}?culture=en");
        Assert.Contains("data-modal-form", form);
        Assert.Contains("data-invoice-confirmation", form);
        var token = WorkspaceUiTests.Token(form);
        Task<System.Net.Http.HttpResponseMessage> Confirm(Invoice invoice, Guid operation, string amount) =>
            WorkspaceUiTests.Post(client, $"/Invoices/Confirm/{invoice.Id}", token,
                ("OperationId", operation.ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
                ("Amount", amount), ("PaymentMethod", "Cash"));

        var operation = Guid.NewGuid();
        using (var excessive = await Confirm(first, Guid.NewGuid(), "8.01"))
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, excessive.StatusCode);
        var repeated = await Task.WhenAll(Confirm(first, operation, "3"), Confirm(first, operation, "3"));
        foreach (var response in repeated)
        {
            using (response)
                Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
        using (var changedRetry = await Confirm(first, operation, "4"))
            Assert.Equal(System.Net.HttpStatusCode.Conflict, changedRetry.StatusCode);
        using (var newOperation = await Confirm(first, Guid.NewGuid(), "3"))
            Assert.Equal(System.Net.HttpStatusCode.Conflict, newOperation.StatusCode);
        db.ChangeTracker.Clear();
        var confirmed = await db.Invoices.AsNoTracking().SingleAsync(invoice => invoice.Id == first.Id);
        Assert.Equal(InvoiceStatus.Confirmed, confirmed.Status);
        Assert.Equal(PaymentStatus.Partial, confirmed.PaymentStatus);
        Assert.Equal(3m, confirmed.PaidAmount);
        Assert.Equal(5m, confirmed.RemainingAmount);
        Assert.NotNull(confirmed.LastActionAt);
        Assert.Single(await db.Payments.Where(payment => payment.InvoiceId == first.Id).ToListAsync());
        var entry = Assert.Single(await db.InvoiceStatusHistory.Where(item => item.InvoiceId == first.Id).ToListAsync());
        Assert.Equal(actor.Id, entry.ActorId);
        Assert.Equal(actor.FullName, entry.ActorFullName);
        Assert.Equal(4m, (await db.Stocks.AsNoTracking().SingleAsync()).ReservedQuantity);

        var competing = await Task.WhenAll(Confirm(left, Guid.NewGuid(), "0"), Confirm(right, Guid.NewGuid(), "0"));
        Assert.Single(competing, response => response.IsSuccessStatusCode);
        Assert.Single(competing, response => response.StatusCode == System.Net.HttpStatusCode.BadRequest);
        foreach (var response in competing) response.Dispose();
        Assert.Equal(9m, (await db.Stocks.AsNoTracking().SingleAsync()).ReservedQuantity);
        Assert.Equal(10m, (await db.Stocks.AsNoTracking().SingleAsync()).Quantity);
        Assert.Equal(2, await db.InvoiceStatusHistory.CountAsync());
        Assert.Single(await db.Payments.AsNoTracking().ToListAsync());
        var detail = await client.GetStringAsync($"/Invoices/Details/{first.Id}?culture=en");
        Assert.Contains("Status history", detail);
        Assert.DoesNotContain($"data-modal-url=\"/Invoices/Confirm/{first.Id}\"", detail);
        var arabic = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync($"/Invoices/Details/{first.Id}?culture=ar"));
        Assert.Contains("سجل الحالات", arabic);
    }

    [PostgresFact]
    public async Task ConfirmationRollsBackStockAndStatusWhenPaymentStorageFails()
    {
        await using var fixture = await Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var db = fixture.Db;
        var user = await db.Users.SingleAsync(item => item.UserName == "ui-admin");
        var invoice = new Invoice
        {
            InvoiceNumber = "FAIL-PAYMENT", CustomerName = "Payment rollback customer", UserId = user.Id, WarehouseId = fixture.Source, TotalAmount = 10,
            RemainingAmount = 10, Items = [new InvoiceItem { ProductId = fixture.Product, Quantity = 2, UnitPrice = 5, TotalAmount = 10 }]
        };
        db.AddRange(invoice, new Stock { ProductId = fixture.Product, WarehouseId = fixture.Source, Quantity = 5 });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_invoice_payment() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected payment storage failure'; END $$;
            CREATE TRIGGER reject_invoice_payment BEFORE INSERT ON "Payments"
            FOR EACH ROW EXECUTE FUNCTION reject_invoice_payment();
            """);
        var form = await client.GetStringAsync($"/Invoices/Confirm/{invoice.Id}");
        using var response = await WorkspaceUiTests.Post(client, $"/Invoices/Confirm/{invoice.Id}", WorkspaceUiTests.Token(form),
            ("OperationId", Guid.NewGuid().ToString()), ("RowVersion", Convert.ToBase64String(invoice.RowVersion)),
            ("Amount", "5"), ("PaymentMethod", "Cash"));
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal(InvoiceStatus.Pending, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(0m, (await db.Stocks.SingleAsync()).ReservedQuantity);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Empty(await db.InvoiceStatusHistory.ToListAsync());
    }

    [PostgresFact]
    public async Task InvoiceCreationUsesServerSnapshotsAndSerializesDailyNumbers()
    {
        await using var fixture = await Database.Create(migrate: true, withReceiptRate: true);
        var warehouse = await fixture.Db.Warehouses.SingleAsync(item => item.Id == fixture.Source);
        warehouse.IsDefault = true;
        var customer = new Customer { Name = "Invoice fixture customer" };
        fixture.Db.Add(customer);
        fixture.Db.Stocks.Add(new Stock { ProductId = fixture.Product, WarehouseId = warehouse.Id, Quantity = 15, ReservedQuantity = 2 });
        await fixture.Db.SaveChangesAsync();

        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: fixture.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var index = await client.GetStringAsync("/Invoices?culture=en");
        Assert.Contains("data-modal-url=\"/Invoices/Create\"", index);
        var form = await client.GetStringAsync("/Invoices/Create?culture=en");
        Assert.Contains("data-modal-form", form);
        Assert.Contains("data-add-invoice-line", form);
        Assert.DoesNotContain("name=\"WarehouseId\"", form);
        Assert.DoesNotContain("name=\"DeliveryMode\"", form);
        var token = WorkspaceUiTests.Token(form);
        Assert.NotEmpty(token);
        Assert.Contains(customer.Name, await client.GetStringAsync("/Invoices/Customers?search=fixture"));
        Assert.Contains(customer.Name, await client.GetStringAsync("/Invoices/Customers"));
        Assert.Contains("\"unitPrice\":4", await client.GetStringAsync("/Invoices/Products"));
        Assert.Contains("\"unitPrice\":4", await client.GetStringAsync("/Invoices/Products?search=product"));

        using var invalid = await Post(Guid.NewGuid(), "New customer", quantity: "14");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
        using var stale = await Post(Guid.NewGuid(), "New customer", unitPrice: "5");
        Assert.Equal(System.Net.HttpStatusCode.Conflict, stale.StatusCode);

        var operations = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var requests = operations.Select((id, i) => Post(id, i == 0 ? customer.Name : "New customer"))
            .Append(Post(operations[0], customer.Name));
        var responses = await Task.WhenAll(requests);
        foreach (var response in responses)
        {
            using (response)
                Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }

        var invoices = await fixture.Db.Invoices.AsNoTracking().Include(invoice => invoice.Items)
            .OrderBy(invoice => invoice.InvoiceNumber).ToListAsync();
        Assert.Equal(4, invoices.Count);
        var prefix = $"INV-{DateTime.Now:MMddyyyy}-";
        Assert.Equal(new[] { "01", "02", "03", "04" }.Select(suffix => prefix + suffix),
            invoices.Select(invoice => invoice.InvoiceNumber));
        foreach (var invoice in invoices)
        {
            Assert.Equal(InvoiceStatus.Pending, invoice.Status);
            Assert.Equal("UI test ui-admin", invoice.CreatedByFullName);
            Assert.Equal(warehouse.Id, invoice.WarehouseId);
            Assert.Equal(8m, invoice.SubTotal);
            Assert.Equal(2m, invoice.DiscountAmount);
            Assert.Equal(6m, invoice.TotalAmount);
            Assert.Equal(6m, invoice.RemainingAmount);
            Assert.Equal(4m, Assert.Single(invoice.Items).UnitPrice);
        }
        Assert.Equal(customer.Id, invoices.Single(invoice => invoice.Id == operations[0]).CustomerId);
        Assert.All(invoices.Where(invoice => invoice.Id != operations[0]), invoice => Assert.Null(invoice.CustomerId));
        Assert.Single(await fixture.Db.Customers.AsNoTracking().ToListAsync());
        Assert.Empty(await fixture.Db.Payments.AsNoTracking().ToListAsync());
        var creator = await fixture.Db.Users.SingleAsync(item => item.UserName == "ui-admin");
        creator.FullName = "Edited account name";
        await fixture.Db.SaveChangesAsync();
        var detail = await client.GetStringAsync($"/Invoices/Details/{operations[0]}?culture=en");
        Assert.Contains("data-invoice-created-by><bdi>UI test ui-admin</bdi>", detail);
        var nextDay = DateOnly.FromDateTime(DateTime.Now).AddDays(1);
        var nextNumber = await new UnitOfWork(fixture.Db).ExecuteReadCommittedAsync(
            ct => new InvoiceNumberGenerator(fixture.Db).NextAsync(nextDay, ct));
        Assert.Equal($"INV-{nextDay:MMddyyyy}-01", nextNumber);

        Task<HttpResponseMessage> Post(Guid id, string name, string quantity = "2", string unitPrice = "4") =>
            WorkspaceUiTests.Post(client, "/Invoices/Create", token, ("OperationId", id.ToString()), ("CustomerName", name),
                ("DiscountType", "Percentage"), ("DiscountValue", "25"), ("Lines[0].ProductId", fixture.Product.ToString()),
                ("Lines[0].Quantity", quantity), ("Lines[0].QuotedUnitPrice", unitPrice));
    }

    [PostgresFact]
    public async Task InvoiceCreationRepairUpgradesTheAlreadyAppliedOriginalSchema()
    {
        await using var fixture = await Database.Create(migrate: true,
            migrationTarget: "20261118122622_UnifyInvoiceSchema");
        var db = fixture.Db;
        var before = await db.Database.GetAppliedMigrationsAsync();
        Assert.DoesNotContain("20261119100000_RepairInvoiceCreationColumns", before);
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Invoices"
                DROP COLUMN "CustomerName",
                DROP COLUMN "CreatedByFullName",
                DROP COLUMN "DiscountType",
                DROP COLUMN "DiscountValue";
            """);
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Invoices.AsNoTracking().ToListAsync());
        Assert.Equal(4, await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::integer AS "Value" FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'Invoices'
            AND column_name IN ('CustomerName', 'CreatedByFullName', 'DiscountType', 'DiscountValue')
            """).SingleAsync());
        await db.Database.MigrateAsync();
        Assert.Contains("20261119100000_RepairInvoiceCreationColumns",
            await db.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task InvoiceSchemaMigrationPreservesSalesRowsAndReferences()
    {
        const string previousMigration = "20261115112800_AddProductPackaging";
        await using var fixture = await Database.Create(migrate: true, migrationTarget: previousMigration);
        var db = fixture.Db;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = "invoice-migration-user", FullName = "Invoice migration",
            BranchId = fixture.Branch
        };
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Historical customer" };
        var supplier = new Supplier { Name = "Historical supplier" };
        db.AddRange(user, supplier);
        await db.SaveChangesAsync();
        // This fixture deliberately targets the schema before Customer.AccountId existed.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Customers" ("Id", "Type", "Name", "Category", "CreditLimit", "CurrentBalance",
                "PaymentTermsDays", "IsActive", "CreatedAt", "RowVersion")
            VALUES ({customer.Id}, {0}, {customer.Name}, {0}, {0m}, {0m}, {0}, {true}, {DateTimeOffset.UtcNow}, {Guid.NewGuid().ToByteArray()})
            """);

        var saleId = Guid.NewGuid();
        var returnId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var supplierPaymentId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var movement = new StockMovement
        {
            ProductId = fixture.Product, WarehouseId = fixture.Source, Quantity = 2,
            ReferenceType = "SalesInvoice", ReferenceId = saleId
        };
        db.Add(movement);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SalesInvoices" (
                "Id", "InvoiceNumber", "CustomerId", "WarehouseId", "UserId", "InvoiceDate", "InvoiceType",
                "SubTotal", "DiscountAmount", "TaxAmount", "TotalAmount", "PaidAmount", "RemainingAmount",
                "ChangeAmount", "PaymentStatus", "Status", "ParentInvoiceId", "RowVersion", "CreatedAt")
            VALUES
                ({saleId}, 'LEGACY-SALE', {customer.Id}, {fixture.Source}, {user.Id}, now(), 0,
                 25, 0, 0, 25, 10, 15, 0, 1, 2, NULL, decode('01', 'hex'), now()),
                ({returnId}, 'LEGACY-RETURN', NULL, {fixture.Source}, {user.Id}, now(), 1,
                 25, 0, 0, 25, 0, 25, 0, 0, 1, {saleId}, decode('02', 'hex'), now());

            INSERT INTO "SalesInvoiceItems" (
                "Id", "InvoiceId", "ProductId", "Quantity", "UnitPrice", "UnitCost", "DiscountAmount",
                "TaxRate", "TaxAmount", "TotalAmount", "CreatedAt")
            VALUES ({itemId}, {saleId}, {fixture.Product}, 2, 12.50, 9, 0, 0, 0, 25, now());

            INSERT INTO "PurchaseInvoices" (
                "Id", "InvoiceNumber", "SupplierId", "WarehouseId", "InvoiceDate", "SubTotal",
                "DiscountAmount", "TaxAmount", "TotalAmount", "PaidAmount", "RemainingAmount",
                "PaymentStatus", "Status", "RowVersion", "CreatedAt")
            VALUES ({purchaseId}, 'LEGACY-PURCHASE', {supplier.Id}, {fixture.Source}, now(),
                15, 0, 0, 15, 15, 0, 2, 1, decode('03', 'hex'), now());

            INSERT INTO "Payments" (
                "Id", "PaymentNumber", "PaymentType", "CustomerId", "SupplierId", "SalesInvoiceId",
                "PurchaseInvoiceId", "Amount", "PaymentMethod", "PaymentDate", "RowVersion", "CreatedAt")
            VALUES
                ({salePaymentId}, 'LEGACY-SALE-PAYMENT', 0, {customer.Id}, NULL, {saleId},
                 NULL, 10, 0, current_date, decode('04', 'hex'), now()),
                ({supplierPaymentId}, 'LEGACY-SUPPLIER-PAYMENT', 1, NULL, {supplier.Id}, NULL,
                 {purchaseId}, 15, 0, current_date, decode('05', 'hex'), now());
            """);

        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync();

        var sale = await db.Invoices.AsNoTracking().Include(invoice => invoice.Items)
            .SingleAsync(invoice => invoice.Id == saleId);
        Assert.Equal("LEGACY-SALE", sale.InvoiceNumber);
        Assert.Equal(InvoiceType.Sales, sale.Type);
        Assert.Equal(0, sale.LegacyInvoiceTypeCode);
        Assert.Equal(2, (int)sale.Status);
        Assert.Equal(new byte[] { 1 }, sale.RowVersion);
        Assert.Equal(25m, sale.TotalAmount);
        var item = Assert.Single(sale.Items);
        Assert.Equal(itemId, item.Id);
        Assert.Equal(12.50m, item.UnitPrice);
        var returned = await db.Invoices.AsNoTracking().SingleAsync(invoice => invoice.Id == returnId);
        Assert.Equal(saleId, returned.ParentInvoiceId);
        Assert.Equal(1, returned.LegacyInvoiceTypeCode);
        Assert.Equal(saleId, (await db.Payments.AsNoTracking().SingleAsync(payment => payment.Id == salePaymentId)).InvoiceId);
        var supplierPayment = await db.Payments.AsNoTracking().SingleAsync(payment => payment.Id == supplierPaymentId);
        Assert.Equal(supplier.Id, supplierPayment.SupplierId);
        Assert.Equal(15m, supplierPayment.Amount);
        Assert.Null(supplierPayment.InvoiceId);
        Assert.Equal(nameof(Invoice), (await db.StockMovements.AsNoTracking().SingleAsync(stock => stock.Id == movement.Id)).ReferenceType);
        Assert.True(await db.Database.SqlQueryRaw<bool>("""
            SELECT (
                to_regclass('"SalesInvoices"') IS NULL AND to_regclass('"SalesInvoiceItems"') IS NULL
                AND to_regclass('"PurchaseInvoices"') IS NULL AND to_regclass('"PurchaseInvoiceItems"') IS NULL
                AND EXISTS (SELECT 1 FROM pg_index
                    WHERE indexrelid = to_regclass('"IX_Invoices_InvoiceNumber"') AND indisunique)
            ) AS "Value"
            """).SingleAsync());

        var newSale = new Invoice
        {
            InvoiceNumber = "NEW-SALE", UserId = user.Id, WarehouseId = fixture.Source
        };
        db.Add(newSale);
        await db.SaveChangesAsync();
        await migrator.MigrateAsync(previousMigration);

        var restoredCodes = await db.Database.SqlQueryRaw<int>("""
            SELECT "InvoiceType" AS "Value" FROM "SalesInvoices"
            """).ToListAsync();
        Assert.Equal(new[] { 0, 0, 1 }, restoredCodes.Order());
    }

    [PostgresFact]
    public async Task RelatedGraphFindsInvoiceReturnsPaymentsJournalsAndStockWithoutDuplicates()
    {
        await using var f = await Database.Create();
        var tag = new Tag
        {
            Name = "Tagged",
            Color = "#cc9900"
        };
        f.Db.Add(tag);
        await f.Db.SaveChangesAsync();
        var customer = new Customer
        {
            Name = "Customer",
            TagId = tag.Id
        };
        var other = new Customer
        {
            Name = "Other",
            TagId = tag.Id
        };
        var supplier = new Supplier
        {
            Name = "Supplier",
            TagId = tag.Id
        };
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "graph-user",
            FullName = "Graph",
            BranchId = f.Branch
        };
        f.Db.AddRange(customer, other, supplier, user);
        await f.Db.SaveChangesAsync();
        var sale = new Invoice
        {
            CustomerId = customer.Id,
            InvoiceNumber = "SALE-LINKED",
            UserId = user.Id,
            WarehouseId = f.Source
        };
        var order = new PurchaseOrder
        {
            SupplierId = supplier.Id,
            Number = "ORDER-LINKED",
            WarehouseId = f.Source
        };
        f.Db.AddRange(sale, order);
        await f.Db.SaveChangesAsync();
        // A return with no CustomerId must still be reached through ParentInvoiceId.
        var returned = new Invoice
        {
            ParentInvoiceId = sale.Id,
            InvoiceNumber = "RETURN-LINKED",
            UserId = user.Id,
            WarehouseId = f.Source,
            LegacyInvoiceTypeCode = 1
        };
        f.Db.Add(returned);
        await f.Db.SaveChangesAsync();
        var payment = new Payment
        {
            CustomerId = customer.Id,
            InvoiceId = sale.Id,
            Amount = 50,
            PaymentNumber = "PAY-LINKED"
        };
        var foreign = new Payment
        {
            CustomerId = other.Id,
            Amount = 99,
            PaymentNumber = "UNRELATED"
        };
        f.Db.AddRange(payment, foreign);
        await f.Db.SaveChangesAsync();
        f.Db.AddRange(new StockMovement
            {
                ProductId = f.Product,
                WarehouseId = f.Source,
                Quantity = 1,
                UnitCost = 10,
                ReferenceType = "Invoice",
                ReferenceId = sale.Id
            });
        await f.Db.SaveChangesAsync();
        var mapping = new Mapster.TypeAdapterConfig();
        mapping.Scan(typeof(NEXT.Application.ApplicationRegistrationExtensions).Assembly);
        var reader = new RelatedRecordsReader(f.Db, mapping);
        var result = await reader.ReadAsync<Customer>(customer.Id, "Payment", 1, default);
        Assert.Single(result.Records);
        Assert.Contains(result.Records[0].Values, x => Equals(x.Value, "PAY-LINKED"));
        Assert.Equal(2, result.Sections.Single(x => x.Key == "Invoice").Count);
        Assert.DoesNotContain(result.Sections, x => x.Key == "JournalEntry");
        Assert.Equal(1, result.Sections.Single(x => x.Key == "StockMovement").Count);
        var supplierActivity = await reader.ReadAsync<Supplier>(supplier.Id, "PurchaseOrder", 1, default);
        Assert.DoesNotContain(supplierActivity.Sections, section => section.Key == "PurchaseInvoice");
        var legacyLink = await reader.ReadAsync<Customer>(customer.Id, "SalesInvoice", 1, default);
        Assert.Equal(nameof(Invoice), legacyLink.Selected);
        Assert.Equal(2, legacyLink.Records.Count);
        Assert.Single(supplierActivity.Records);
        Assert.Equal(1, supplierActivity.Sections.Single(x => x.Key == "PurchaseOrder").Count);
        // Server-side filters/sorting execute on PostgreSQL, not on the current page in memory.
        var customers = new CustomerRepository(f.Db);
        var filtered = customers.Query(customers.BuildFilterExpression(
            new NEXT.Application.QueryModels.CustomerQueryModel
            {
                SearchTerm = "Customer",
                TagId = tag.Id,
                IsActive = true
            }));
        Assert.Equal(customer.Id, (await filtered.SingleAsync()).Id);
    }
    [PostgresFact]
    public async Task ProfileSchemaUpgradeFixesTheMissingAvatarColumnRegression()
    {
        await using var f = await Database.Create();
        await f.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Customers\" DROP COLUMN \"AvatarUrl\"; ALTER TABLE \"Suppliers\" DROP COLUMN \"AvatarUrl\";");
        var query = new GetEntitiesQueryHandler<Customer, CustomerDto>(new CustomerRepository(f.Db), new PaginationServices(), new DataAnnotationsEntityValidator<PaginationQuery>(), new TypeAdapterConfig());
        var error = await Assert.ThrowsAsync<PostgresException>(() => query.Handle(new(new()), default));
        Assert.Equal("42703", error.SqlState);
        var sql = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/sql/20261028_partner_profiles.sql")));
        await f.Db.Database.ExecuteSqlRawAsync(sql);
        await f.Db.Database.ExecuteSqlRawAsync(sql); // The upgrade is safe to repeat.
        Assert.Equal(0, (await query.Handle(new(new()), default)).TotalCount);
        Assert.Equal(0, await f.Db.Suppliers.CountAsync());
    }
    [PostgresFact]
    public async Task PermanentDeletionAndPermissionRevocationPersistReasonsInAudit()
    {
        await using var f = await Database.Create();
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: f.TestConnection);
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
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
    public async Task RepositoryExpressionsUseProductRangesAndGeneralStockSortingOnPostgres()
    {
        await using var f = await Database.Create();
        var product = await f.Db.Products.SingleAsync();
        product.CostPrice = 10;
        product.RetailPrice = 25;
        product.MinStock = 5;
        product.MaxStock = 100;
        product.HasExpiry = true;
        f.Db.Stocks.Add(new Stock
        {
            ProductId = f.Product,
            WarehouseId = f.Source,
            Quantity = 20,
            ReservedQuantity = 6,
            AverageCost = 10
        });
        await f.Db.SaveChangesAsync();
        var productRepository = new ProductRepository(f.Db);
        var products = productRepository.Query(productRepository.BuildFilterExpression(new NEXT.Application.QueryModels.ProductQueryModel
        {
            MinCostPrice = 9,
            MaxRetailPrice = 30,
            MinStock = 4,
            MaxStock = 101,
            HasExpiry = true
        }));
        Assert.Single(await products.ToListAsync());
        var stockRepository = new StockRepository(f.Db);
        var stocks = stockRepository.ApplySorting(stockRepository.Query(stockRepository.BuildFilterExpression(new PaginationQuery())), "Quantity", SortDirection.Descending);
        Assert.Equal(14, (await stocks.SingleAsync()).Quantity - (await stocks.SingleAsync()).ReservedQuantity);
        var excluded = productRepository.Query(productRepository.BuildFilterExpression(new NEXT.Application.QueryModels.ProductQueryModel
        {
            MinCostPrice = 11
        }));
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

    internal sealed class Database : IAsyncDisposable
    {
        private readonly string name = "next_tests_" + Guid.NewGuid().ToString("N");
        private readonly string adminConnection;
        private string Connection => new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Database = name
        }.ConnectionString;
        public string TestConnection => Connection;
        public Guid Branch { get; private set; }
        public NEXTDbContext Db { get; private set; } = null!;
        public Guid Product { get; private set; }
        public Guid Source { get; private set; }
        public Guid Target { get; private set; }
        private Database(string connection) => adminConnection = new NpgsqlConnectionStringBuilder(connection)
        {
            Database = "postgres",
            Pooling = false
        }.ConnectionString;
        public static async Task<Database> Create(bool expiry = false, bool migrate = false,
            bool preupgraded = false, bool withReceiptRate = false, string? migrationTarget = null)
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
                if (migrate)
                {
                    if (preupgraded)
                    {
                        await database.Db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20261027072142_UseApplicationManagedConcurrencyTokens");
                        foreach (var file in new[] { "20261028_inventory_constraints.sql", "20261028_partner_profiles.sql" })
                        {
                            await database.Db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/sql", file))));
                        }
                        var script = database.Db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript(
                                                    "20261027072142_UseApplicationManagedConcurrencyTokens", null, Microsoft.EntityFrameworkCore.Migrations.MigrationsSqlGenerationOptions.Idempotent);
                        await database.Db.Database.ExecuteSqlRawAsync(script);
                        await database.Db.Database.ExecuteSqlRawAsync(script);
                    }
                    await database.Db.GetService<IMigrator>().MigrateAsync(migrationTarget);
                }
                else
                {
                    await database.Db.Database.EnsureCreatedAsync();
                }
                var product = new Product
                {
                    Name = "Test product",
                    HasExpiry = expiry,
                    CostPrice = withReceiptRate ? 4 : 0
                };
                var source = new Warehouse
                {
                    Name = "Source",
                    Code = "S"
                };
                var target = new Warehouse
                {
                    Name = "Target",
                    Code = "T"
                };
                var branch = new Branch
                {
                    Name = "Test branch"
                };
                database.Db.AddRange(product, source, target, branch);
                if (withReceiptRate)
                {
                    database.Db.Add(new SystemSetting { Key = ExchangeRateReader.SettingKey, ValueJson = "1", Category = "Currency" });
                }
                await database.Db.SaveChangesAsync();
                database.Branch = branch.Id;
                database.Product = product.Id;
                database.Source = source.Id;
                database.Target = target.Id;
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }
        public NEXTDbContext NewContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
        {
            var context = new TestContext();
            var serializer = new AuditValueSerializer();
            return new NEXTDbContext(new DbContextOptionsBuilder<NEXTDbContext>().UseNpgsql(Connection).AddInterceptors(interceptors).Options,
                new TrackedChangesProcessor(context, serializer, new SoftDeleteHandler(serializer), new AuditFieldsHandler()),
                new AuditEntryFactory(context, context, serializer));
        }
        public InventoryService Service(NEXTDbContext? context = null)
        {
            var db = context ?? Db;
            return new(new KeyRepository<Product, Guid>(db), new KeyRepository<Warehouse, Guid>(db), new TRepository<Stock>(db),
                new TRepository<StockMovement>(db), new TRepository<ProductBatch>(db), new UnitOfWork(db),
                new DataAnnotationsEntityValidator<ReceiveStockDto>(), new DataAnnotationsEntityValidator<TransferStockDto>(),
                new ExchangeRateReader(new SystemSettingRepository(db)), new InvoiceConfirmationStore(db));
        }
        public async Task SetProductCostAsync(decimal cost)
        {
            var product = await Db.Products.SingleAsync(product => product.Id == Product);
            product.CostPrice = cost;
            await Db.SaveChangesAsync();
        }
        public ReceiveStockDto Receipt(decimal quantity) => new()
        {
            ProductId = Product,
            WarehouseId = Source,
            Quantity = quantity
        };
        public TransferStockDto Transfer(decimal quantity) => new()
        {
            ProductId = Product,
            SourceWarehouseId = Source,
            DestinationWarehouseId = Target,
            Quantity = quantity
        };
        public async ValueTask DisposeAsync()
        {
            if (Db is not null)
            {
                await Db.DisposeAsync();
            }
            await using var admin = new NpgsqlConnection(adminConnection);
            await admin.OpenAsync();
            // Only this fixture's generated database name is ever dropped.
            if (!name.StartsWith("next_tests_", StringComparison.Ordinal) || name.Length != 43)
            {
                throw new InvalidOperationException("Invalid test database name.");
            }
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
