using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net;
using System.Text.Json;
using NEXT.Application.DTOs.Products;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Features.Currencies;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using Xunit;

namespace NEXT.Tests;

public sealed class ProductPercentageTests
{
    [Theory]
    [InlineData("MinStock", "0.0004")]
    [InlineData("MaxStock", "2.0004")]
    [InlineData("TaxRate", "1.234")]
    public async Task UnsupportedDecimalPrecisionCannotCreateOrChangeAProduct(string field, string value)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var input = new ProductInputDto { Name = "Precision product", MinStock = 0.001m, MaxStock = 2.001m, TaxRate = 1.23m };
        var saved = await sender.Send(new CreateEntityCommand<ProductDto, ProductInputDto>(input));
        var invalid = decimal.Parse(value, CultureInfo.InvariantCulture);
        switch (field)
        {
            case "MinStock": input.MinStock = invalid; break;
            case "MaxStock": input.MaxStock = invalid; break;
            case "TaxRate": input.TaxRate = invalid; break;
        }

        var createError = await Assert.ThrowsAsync<AppValidationException>(() =>
            sender.Send(new CreateEntityCommand<ProductDto, ProductInputDto>(input)));
        Assert.True(createError.Errors.ContainsKey(field));
        var updateError = await Assert.ThrowsAsync<AppValidationException>(() =>
            sender.Send(new UpdateEntityCommand<ProductDto, ProductInputDto>(saved.Id, input, saved.RowVersion)));
        Assert.True(updateError.Errors.ContainsKey(field));
        var stored = Assert.Single(await db.Products.AsNoTracking().ToListAsync());
        Assert.Equal(0.001m, stored.MinStock);
        Assert.Equal(2.001m, stored.MaxStock);
        Assert.Equal(1.23m, stored.TaxRate);
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("en")]
    public async Task ProductPrecisionAndStockRangeErrorsAreLocalizedOverHttp(string culture)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var token = WorkspaceUiTests.Token(await client.GetStringAsync($"/Products/Create?culture={culture}"));
        var arabic = culture == "ar";
        foreach (var (field, value, message) in new[]
        {
            ("MinStock", "0.0004", arabic ? "استخدم ثلاث منازل عشرية كحد أقصى لكميات المخزون." : "Use at most three decimal places for stock quantities."),
            ("MaxStock", "0.0004", arabic ? "استخدم ثلاث منازل عشرية كحد أقصى لكميات المخزون." : "Use at most three decimal places for stock quantities."),
            ("TaxRate", "1.234", arabic ? "استخدم منزلتين عشريتين كحد أقصى لنسبة الضريبة." : "Use at most two decimal places for the tax rate."),
            ("MinStock", "10", arabic ? "يجب أن يكون الحد الأعلى للمخزون أكبر من الحد الأدنى أو مساويًا له." : "Maximum stock must be at least the minimum.")
        })
        {
            var fields = new Dictionary<string, string> { ["Name"] = "Invalid precision", ["MinStock"] = "0", ["MaxStock"] = "5", ["TaxRate"] = "0" };
            fields[field] = value;
            using var response = await WorkspaceUiTests.Post(client, $"/Products/Create?culture={culture}", token,
                fields.Select(pair => (pair.Key, pair.Value)).ToArray());
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var errorField = value == "10" ? "MaxStock" : field;
            Assert.Equal(message, problem.RootElement.GetProperty("errors").GetProperty(errorField)[0].GetString());
        }
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<NEXTDbContext>().Products.ToListAsync());
    }

    [PostgresFact]
    public async Task SupportedProductDecimalPrecisionSurvivesPostgresAndInvalidUpdatesLeaveItUnchanged()
    {
        await using var database = await PostgresInventoryTests.Database.Create(migrate: true);
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: database.TestConnection);
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var input = new ProductInputDto { Name = "Postgres precision", MinStock = 0.001000m, MaxStock = 2.001000m, TaxRate = 1.23000m };
        var saved = await sender.Send(new CreateEntityCommand<ProductDto, ProductInputDto>(input));
        var updated = await sender.Send(new UpdateEntityCommand<ProductDto, ProductInputDto>(saved.Id, input, saved.RowVersion));
        input.MinStock = 0.0004m;
        await Assert.ThrowsAsync<AppValidationException>(() =>
            sender.Send(new UpdateEntityCommand<ProductDto, ProductInputDto>(saved.Id, input, updated.RowVersion)));
        var stored = await database.Db.Products.AsNoTracking().SingleAsync(row => row.Id == saved.Id);
        Assert.Equal(0.001m, stored.MinStock);
        Assert.Equal(2.001m, stored.MaxStock);
        Assert.Equal(1.23m, stored.TaxRate);
    }

    [Fact]
    public async Task ComputedPricesAreSortedAndFilteredBeforePagingAndEditReadsPercentages()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var first = await Create(sender, "High percentage", 100, 100);
        var second = await Create(sender, "Low percentage", 200, 10);
        foreach (var field in new[] { "RetailPrice", "WholesalePrice", "VipPrice" })
        {
            var query = new ProductQueryModel { SortBy = field, PageSize = 1 };
            var page = await sender.Send(new GetEntitiesQuery<ProductDto>(query));
            Assert.Equal(2, page.TotalCount);
            Assert.Equal(first.Id, Assert.Single(page.Data).Id);
            query.PageNumber = 2;
            Assert.Equal(second.Id, Assert.Single((await sender.Send(new GetEntitiesQuery<ProductDto>(query))).Data).Id);
            query.PageNumber = 1;
            query.SortDirection = SortDirection.Descending;
            Assert.Equal(second.Id, Assert.Single((await sender.Send(new GetEntitiesQuery<ProductDto>(query))).Data).Id);
        }

        var rate = await sender.Send(new GetExchangeRateQuery());
        await sender.Send(new SetExchangeRateCommand(new() { UsdToSdg = 2, RowVersion = rate.RowVersion }));
        var filters = new[]
        {
            new ProductQueryModel { MinRetailPrice = 420, MaxRetailPrice = 450 },
            new ProductQueryModel { MinWholesalePrice = 420, MaxWholesalePrice = 450 },
            new ProductQueryModel { MinVipPrice = 420, MaxVipPrice = 450 }
        };
        foreach (var filter in filters)
        {
            Assert.Equal(second.Id, Assert.Single((await sender.Send(new GetEntitiesQuery<ProductDto>(filter))).Data).Id);
        }
        var edit = await sender.Send(new GetEntityByIdQuery<ProductEditDto>(second.Id));
        Assert.Equal(10, edit.RetailPrice);
        Assert.Equal(10, edit.WholesalePrice);
        Assert.Equal(10, edit.VipPrice);
        Assert.Equal(400, edit.CostPrice);
        edit.CostPrice = 600;
        var result = await sender.Send(new UpdateEntityCommand<ProductDto, ProductInputDto>(edit.Id, edit, edit.RowVersion));
        Assert.Equal(660, result.RetailPrice);
        Assert.Equal(660, result.WholesalePrice);
        Assert.Equal(660, result.VipPrice);
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var stored = await db.Products.AsNoTracking().SingleAsync(row => row.Id == second.Id);
        Assert.Equal(300, stored.CostPrice);
        Assert.Equal(10, stored.RetailPrice);

        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var form = await client.GetStringAsync($"/Products/Edit/{edit.Id}?culture=en");
        Assert.Contains("Retail markup (%)", form);
        Assert.Contains("data-product-pricing", form);
        Assert.Contains("data-price-preview=\"VipPrice\"", form);
        Assert.Matches("name=\"RetailPrice\"[^>]*value=\"10\"", form);
    }

    [PostgresFact]
    public async Task MigrationConvertsAllThreePricesIncludingDeletedProductsAndSqlReadsComputedPrices()
    {
        await using var database = await PostgresInventoryTests.Database.Create(migrate: true);
        var db = database.Db;
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261105120000_SupportUsdProductCost");
        var rowVersion = Guid.NewGuid().ToByteArray();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Products\" SET \"CostPrice\" = 20, \"RetailPrice\" = 250, \"WholesalePrice\" = 230, \"VipPrice\" = 220, \"RowVersion\" = {rowVersion} WHERE \"Id\" = {database.Product}");
        db.Add(new SystemSetting { Key = "Currency.UsdToSdg", ValueJson = "10" });
        await db.SaveChangesAsync();
        var deleted = new Product { Name = "Deleted legacy percentage", CostPrice = 20, RetailPrice = 250, WholesalePrice = 230, VipPrice = 220, DeletedAt = DateTimeOffset.UtcNow };
        db.Add(deleted);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await migrator.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        foreach (var product in await db.Products.IgnoreQueryFilters().AsNoTracking().ToListAsync())
        {
            Assert.Equal(25, product.RetailPrice);
            Assert.Equal(15, product.WholesalePrice);
            Assert.Equal(10, product.VipPrice);
        }
        var stored = await db.Products.AsNoTracking().SingleAsync();
        Assert.False(stored.RowVersion.SequenceEqual(rowVersion));
        await using var app = await WorkspaceUiTests.StartAsync(postgresConnection: database.TestConnection);
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new GetEntitiesQuery<ProductDto>(new ProductQueryModel
        {
            MinRetailPrice = 249,
            MaxWholesalePrice = 231,
            MinVipPrice = 219,
            SortBy = "RetailPrice",
            PageSize = 1
        }));
        Assert.Equal(250, Assert.Single(result.Data).RetailPrice);
        Assert.Equal(25, (await sender.Send(new GetEntityByIdQuery<ProductEditDto>(stored.Id))).RetailPrice);
    }

    [PostgresFact]
    public async Task MigrationRejectsZeroCostSalesWithoutPartiallyConvertingPrices()
    {
        await using var database = await PostgresInventoryTests.Database.Create(migrate: true);
        var db = database.Db;
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261105120000_SupportUsdProductCost");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Products\" SET \"CostPrice\" = 0, \"RetailPrice\" = 10 WHERE \"Id\" = {database.Product}");
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync());
        Assert.Equal(10, (await db.Products.AsNoTracking().SingleAsync()).RetailPrice);
        Assert.DoesNotContain("20261106120000_UseProductSalePercentages", await db.Database.GetAppliedMigrationsAsync());
    }

    private static Task<ProductDto> Create(ISender sender, string name, decimal cost, decimal percent) =>
        sender.Send(new CreateEntityCommand<ProductDto, ProductInputDto>(new()
        {
            Name = name,
            CostPrice = cost,
            RetailPrice = percent,
            WholesalePrice = percent,
            VipPrice = percent
        }));
}
