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

namespace NEXT.Tests;

public sealed class SharedCqrsRepositoryTests
{
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
