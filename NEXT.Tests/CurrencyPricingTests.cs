using System.Net;
using System.Net.Http.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Inventory;
using NEXT.Application.DTOs.Products;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Currencies;
using NEXT.Application.QueryModels;
using NEXT.Application.Services.Currencies;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class CurrencyPricingTests
{
    [Fact]
    public async Task ProductCostsAreStoredInUsdAndReadFilteredAndEditedInSdg()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var rate = await SetRate(sender, 10);
        var product = await sender.Send(new CreateEntityCommand<ProductDto, ProductInputDto>(new()
        {
            Name = "Currency product",
            CostPrice = 200,
            RetailPrice = 25,
            WholesalePrice = 15,
            VipPrice = 10,
            ExchangeRate = 10
        }));
        Assert.Equal(200, product.CostPrice);
        Assert.Equal(20, (await db.Products.AsNoTracking().SingleAsync(x => x.Id == product.Id)).CostPrice);

        await sender.Send(new SetExchangeRateCommand(new() { UsdToSdg = 20, RowVersion = rate.RowVersion }));
        var read = await sender.Send(new GetEntityByIdQuery<ProductDto>(product.Id));
        Assert.Equal(400, read.CostPrice);
        Assert.Equal(20, read.ExchangeRate);
        Assert.Equal(500, read.RetailPrice);
        Assert.Equal(460, read.WholesalePrice);
        Assert.Equal(440, read.VipPrice);
        var filter = new ProductQueryModel { MinCostPrice = 390, MaxCostPrice = 410 };
        Assert.Equal(product.Id, Assert.Single((await sender.Send(new GetEntitiesQuery<ProductDto>(filter))).Data).Id);
        Assert.Equal(390, filter.MinCostPrice);
        filter.MaxCostPrice = 399;
        Assert.Empty((await sender.Send(new GetEntitiesQuery<ProductDto>(filter))).Data);

        var updated = await sender.Send(new UpdateEntityCommand<ProductDto, ProductInputDto>(product.Id, new()
        {
            Name = "Renamed product",
            CostPrice = read.CostPrice,
            RetailPrice = 25,
            WholesalePrice = 15,
            VipPrice = 10,
            ExchangeRate = read.ExchangeRate
        }, read.RowVersion));
        Assert.Equal(400, updated.CostPrice);
        Assert.Equal(20, (await db.Products.AsNoTracking().SingleAsync(x => x.Id == product.Id)).CostPrice);
        await Assert.ThrowsAsync<ConflictException>(() => sender.Send(new UpdateEntityCommand<ProductDto, ProductInputDto>(
            product.Id, new() { Name = "Stale price", CostPrice = 200, ExchangeRate = 10 }, updated.RowVersion)));

        var warehouse = new Warehouse { Name = "Currency warehouse", Code = "CUR" };
        db.Add(warehouse);
        db.Add(new Stock { ProductId = product.Id, Warehouse = warehouse, Quantity = 500, AverageCost = 7 });
        await db.SaveChangesAsync();
        var inventory = scope.ServiceProvider.GetRequiredService<NEXT.Application.Abstractions.IServices.IInventoryQueryService>();
        var stock = Assert.Single((await inventory.StocksAsync(new PaginationQuery())).Data);
        Assert.Equal(400, stock.CostPrice);
        Assert.Equal(7, stock.AverageCost);
        Assert.Equal(500, stock.Quantity);
    }

    [Fact]
    public async Task FirstRateConvertsLegacyAndDeletedProductsExactlyOnce()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        db.Remove(await db.SystemSettings.SingleAsync(x => x.Key == ExchangeRateReader.SettingKey));
        var active = new Product { Name = "Legacy", CostPrice = 200 };
        var deleted = new Product { Name = "Deleted legacy", CostPrice = 300, DeletedAt = DateTimeOffset.UtcNow };
        db.AddRange(active, deleted);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var rate = await SetRate(sender, 10);
        Assert.Equal(20, (await db.Products.AsNoTracking().SingleAsync(x => x.Id == active.Id)).CostPrice);
        Assert.Equal(30, (await db.Products.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == deleted.Id)).CostPrice);
        await sender.Send(new SetExchangeRateCommand(new() { UsdToSdg = 20, RowVersion = rate.RowVersion }));
        Assert.Equal(20, (await db.Products.AsNoTracking().SingleAsync(x => x.Id == active.Id)).CostPrice);
        Assert.Equal(400, (await sender.Send(new GetEntityByIdQuery<ProductDto>(active.Id))).CostPrice);
    }

    [Fact]
    public async Task CurrencyEndpointRequiresAdministratorAndRejectsStaleOrInvalidUpdates()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var reader = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(reader, "ui-reader");
        reader.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/Currency")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/Currency/Settings")).StatusCode);
        using var admin = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(admin, "ui-admin");
        var current = await admin.GetFromJsonAsync<ExchangeRateDto>("/Currency");
        var html = await admin.GetStringAsync("/Currency/Settings?culture=en");
        Assert.Contains("Currency settings", html);
        Assert.Contains("data-currency-settings", html);
        Assert.Contains("name=\"UsdToSdg\"", html);
        Assert.Contains("currency-settings.js", html);
        Assert.Contains("/Currency/Settings", html);
        Assert.Contains("data-currency-initial", html);
        var arabic = await admin.GetStringAsync("/Currency/Settings?culture=ar");
        Assert.Contains("إعدادات العملات", System.Net.WebUtility.HtmlDecode(arabic));
        var token = WorkspaceUiTests.Token(html);
        var version = Convert.ToBase64String(current!.RowVersion);
        var updated = await WorkspaceUiTests.Post(admin, "/Currency", token, ("UsdToSdg", "10"), ("RowVersion", version));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(10, (await updated.Content.ReadFromJsonAsync<ExchangeRateDto>())!.UsdToSdg);
        Assert.Equal(HttpStatusCode.Conflict,
            (await WorkspaceUiTests.Post(admin, "/Currency", token, ("UsdToSdg", "20"), ("RowVersion", version))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await WorkspaceUiTests.Post(admin, "/Currency", token, ("UsdToSdg", "0"), ("RowVersion", version))).StatusCode);
    }

    private static async Task<ExchangeRateDto> SetRate(ISender sender, decimal rate)
    {
        var current = await sender.Send(new GetExchangeRateQuery());
        return await sender.Send(new SetExchangeRateCommand(new() { UsdToSdg = rate, RowVersion = current.RowVersion }));
    }

    [PostgresFact]
    public async Task PostgreSqlRetainsEightDecimalPlacesAndMatchesTheMigrationSnapshot()
    {
        await using var database = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: database.TestConnection);
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        await SetRate(sender, 3);
        var product = await sender.Send(new CreateEntityCommand<ProductDto, ProductInputDto>(new()
        {
            Name = "Fractional USD cost",
            CostPrice = 0.01m,
            ExchangeRate = 3
        }));
        Assert.Equal(0.00333333m, (await db.Products.AsNoTracking().SingleAsync(x => x.Id == product.Id)).CostPrice);
        var read = await sender.Send(new GetEntityByIdQuery<ProductDto>(product.Id));
        await sender.Send(new UpdateEntityCommand<ProductDto, ProductInputDto>(product.Id, new()
        {
            Name = "Edited fractional cost",
            CostPrice = read.CostPrice,
            ExchangeRate = read.ExchangeRate
        }, read.RowVersion));
        Assert.Equal(0.00333333m, (await db.Products.AsNoTracking().SingleAsync(x => x.Id == product.Id)).CostPrice);
    }
}
