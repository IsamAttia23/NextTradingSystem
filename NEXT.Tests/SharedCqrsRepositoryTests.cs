using NEXT.web.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.Tags;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using Xunit;
using NEXT.Application.DTOs.Suppliers;
using NEXT.Application.Services.Inventory;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Repositories;
using NEXT.Infrastructure.Persistence.Repositories.Entities;

namespace NEXT.Tests;

public sealed class SharedCqrsRepositoryTests
{
    [Fact]
    public async Task SupplierSearchCombinesTextAndFiltersBeforeSortingAndPaging()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var tag = new Tag { Name = "Supplier query tag", Color = "#102030" };
        db.Add(tag);
        await db.SaveChangesAsync();
        db.AddRange(
            new Supplier { Name = "Needle Z", Code = "A", IsActive = true, TagId = tag.Id },
            new Supplier { Name = "Needle A", Code = "B", IsActive = true, TagId = tag.Id },
            new Supplier { Name = "Needle B", Code = "C", IsActive = true, TagId = tag.Id },
            new Supplier { Name = "Needle inactive", Code = "D", IsActive = false, TagId = tag.Id },
            new Supplier { Name = "Other", Code = "E", IsActive = true, TagId = tag.Id });
        await db.SaveChangesAsync();

        var repository = scope.ServiceProvider.GetRequiredService<ISupplierRepository>();
        var query = new SupplierQueryModel
        {
            SearchTerm = " Needle ", IsActive = true, SortBy = "Code",
            SortDirection = SortDirection.Descending, PageNumber = 2, PageSize = 1
        };
        var entities = repository.FindAsync(repository.BuildFilterExpression(query));
        Assert.Equal(3, await entities.CountAsync());
        var ordered = repository.ApplySorting(entities, query.SortBy, query.SortDirection);
        Assert.Equal(new[] { "C", "B", "A" }, await ordered.Select(x => x.Code).ToArrayAsync());

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var pagedList = await sender.Send(new GetEntitiesQuery<SupplierDto>(query));
        Assert.Equal(3, pagedList.TotalCount);
        Assert.Equal(2, pagedList.PageNumber);
        Assert.Equal("B", Assert.Single(pagedList.Data).Code);

        query.SortBy = "Name";
        query.SortDirection = SortDirection.Ascending;
        query.PageNumber = 1;
        pagedList = await sender.Send(new GetEntitiesQuery<SupplierDto>(query));
        Assert.Equal("Needle A", Assert.Single(pagedList.Data).Name);
        query.SearchTerm = " ";
        query.IsActive = false;
        pagedList = await sender.Send(new GetEntitiesQuery<SupplierDto>(query));
        Assert.Equal("Needle inactive", Assert.Single(pagedList.Data).Name);
    }

    [Fact]
    public async Task InventoryQueriesApplyPagingToEntitiesBeforeProjectingRelatedNames()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<NEXTDbContext>();
        var warehouse = new Warehouse { Name = "Needle warehouse", Code = "QUERY-W" };
        var product = new Product { Name = "Needle product" };
        db.AddRange(warehouse, product);
        await db.SaveChangesAsync();
        db.AddRange(
            new Stock { ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 8 },
            new ProductBatch { ProductId = product.Id, WarehouseId = warehouse.Id, BatchNumber = "Batch", Quantity = 3 },
            new StockMovement { ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 2, UnitCost = 1 });
        await db.SaveChangesAsync();
        var service = new InventoryQueryService(services.GetRequiredService<IStockRepository>(),
            services.GetRequiredService<IStockMovementRepository>(), services.GetRequiredService<IProductBatchRepository>(),
            new PaginationServices(), new NEXT.Application.Validation.DataAnnotationsEntityValidator<PaginationQuery>());
        Assert.Single((await service.StocksAsync(new PaginationQuery { SearchTerm = "warehouse", PageSize = 1 })).Data);
        Assert.Single((await service.BatchesAsync(new PaginationQuery { SearchTerm = "product", PageSize = 1 })).Data);
        Assert.Single((await service.MovementsAsync(new StockMovementQueryModel { ProductId = product.Id, SearchTerm = "Needle", PageSize = 1 })).Data);
        Assert.Empty((await service.MovementsAsync(new StockMovementQueryModel { ProductId = Guid.NewGuid(), SearchTerm = "Needle" })).Data);
    }

    [Fact]
    public void AllRepositoryExpressionsTranslateToPostgresWithoutExecutingOrPaging()
    {
        using var db = new NEXTDbContext(new DbContextOptionsBuilder<NEXTDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=unused;Password=unused").Options, null!, null!);

        void Check<TEntity>(IRepository<TEntity> repository, PaginationQuery query) where TEntity : class
        {
            query.SearchTerm = "needle";
            query.PageNumber = 2;
            query.PageSize = 1;
            var expression = repository.BuildFilterExpression(query);
            var entities = repository.FindAsync(expression);
            var sql = entities.ToQueryString();
            Assert.Contains("needle", sql);
            Assert.DoesNotContain("LIMIT", sql);
            Assert.DoesNotContain("OFFSET", sql);
            var ordered = repository.ApplySorting(entities, "CreatedAt", SortDirection.Descending);
            Assert.Contains("ORDER BY", ordered.ToQueryString());
            var pageSql = ordered.Skip(1).Take(1).ToQueryString();
            Assert.Contains("LIMIT", pageSql);
            Assert.Contains("OFFSET", pageSql);
        }

        Check(new CustomerRepository(db), new CustomerQueryModel { IsActive = false, MinCreditLimit = 1, TaxNumber = "tax", TagId = Guid.NewGuid() });
        Check(new ProductRepository(db), new ProductQueryModel { Barcode = "code", MaxStock = 10, MinCostPrice = 1, HasExpiry = true });
        Check(new SupplierRepository(db), new SupplierQueryModel { IsActive = true });
        Check(new PurchaseInvoiceRepository(db), new PurchaseInvoiceQueryModel { InvoiceNumber = "number", MaxTotalAmount = 50, SupplierId = Guid.NewGuid() });
        Check(new PurchaseOrderRepository(db), new PurchaseOrderQueryModel { From = new DateOnly(2026, 1, 1), SupplierId = Guid.NewGuid() });
        Check(new SalesInvoiceRepository(db), new SalesInvoiceQueryModel { CustomerName = "customer", From = DateTimeOffset.UtcNow });
        Check(new JournalEntryRepository(db), new JournalEntryQueryModel { To = new DateOnly(2026, 12, 31) });
        Check(new PaymentRepository(db), new PaymentQueryModel { CustomerName = "customer", SupplierName = "supplier" });
        Check(new AuditLogRepository(db), new AuditLogQueryModel { UserId = Guid.NewGuid(), EntityType = "Product" });
        Check(new StockMovementRepository(db), new StockMovementQueryModel { WarehouseId = Guid.NewGuid(), From = DateTimeOffset.UtcNow });
        Check(new StockRepository(db), new PaginationQuery());
        Check(new ProductBatchRepository(db), new PaginationQuery());
        Check(new TagRepository(db), new PaginationQuery());
    }

    [Fact]
    public async Task GenericContractsResolveDedicatedRepositoriesAndSharedQueriesUseTheirFilters()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        Assert.Same(services.GetRequiredService<ICustomerRepository>(), services.GetRequiredService<IKeyRepository<Customer, Guid>>());
        Assert.Same(services.GetRequiredService<IProductRepository>(), services.GetRequiredService<IRepository<Product>>());
        Assert.Same(services.GetRequiredService<IStockMovementRepository>(), services.GetRequiredService<IRepository<StockMovement>>());
        var db = services.GetRequiredService<NEXT.Infrastructure.Persistence.NEXTDbContext>();
        var tag = new Tag { Name = "Query tag", Color = "#102030" }; db.Add(tag); await db.SaveChangesAsync();
        db.AddRange(new Customer { Name = "Allowed", TagId = tag.Id, CreditLimit = 100 },
            new Customer { Name = "Excluded", TagId = tag.Id, CreditLimit = 10 }); await db.SaveChangesAsync();
        var sender = services.GetRequiredService<ISender>();
        var result = await sender.Send(new GetEntitiesQuery<CustomerDto>(new CustomerQueryModel { MinCreditLimit = 50, PageSize = 1 }));
        Assert.Equal(1, result.TotalCount); Assert.Equal("Allowed", Assert.Single(result.Data).Name);
        var excluded = await sender.Send(new GetEntitiesQuery<CustomerDto>(new CustomerQueryModel { SearchTerm = "missing" }));
        Assert.Empty(excluded.Data);
    }

    [Fact]
    public async Task EntitiesWithoutSpecializedModelsUseCommonSearchSortAndPagingThroughCqrs()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        foreach (var name in new[] { "Common A", "Common C", "Other", "Common B" })
            await sender.Send(new CreateEntityCommand<TagDto, TagInputDto>(new() { Name = name }));
        var query = new PaginationQuery { SearchTerm = "Common", SortBy = "Name", SortDirection = SortDirection.Descending, PageSize = 1 };
        var first = await sender.Send(new GetEntitiesQuery<TagDto>(query));
        Assert.Equal(3, first.TotalCount); Assert.Equal("Common C", Assert.Single(first.Data).Name);
        query.PageNumber = 2;
        var second = await sender.Send(new GetEntitiesQuery<TagDto>(query));
        var tag = Assert.Single(second.Data); Assert.Equal("Common B", tag.Name);
        await sender.Send(new UpdateEntityCommand<TagDto, TagInputDto>(tag.Id, new() { Name = "Changed" }));
        Assert.Equal("Changed", (await sender.Send(new GetEntityByIdQuery<TagDto>(tag.Id)))!.Name);
        await sender.Send(new DeleteEntityCommand<TagDto>(tag.Id, "Shared CQRS test"));
        Assert.Equal(2, (await sender.Send(new GetEntitiesQuery<TagDto>(query))).TotalCount);
        using var client = WorkspaceUiTests.Client(app); await WorkspaceUiTests.Login(client, "ui-admin");
        var html = await client.GetStringAsync("/Tags");
        Assert.Contains("name=\"SearchTerm\"", html);
        Assert.DoesNotContain("name=\"IsActive\"", html);
        Assert.DoesNotContain("name=\"Color\"", html);
    }
}
