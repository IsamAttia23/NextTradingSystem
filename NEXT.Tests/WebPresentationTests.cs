using NEXT.web.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Common;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.DTOs.Products;
using System.Net;
using System.Text.RegularExpressions;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class WebPresentationTests
{
    [Theory]
    [InlineData(true, "Yes", "Active")]
    [InlineData(false, "No", "Inactive")]
    public async Task BooleanDetailsAndFiltersDistinguishFlagsFromRecordStatus(bool value, string flag, string status)
    {
        var presenter = new FormFieldPresenter(new CountingReferences());
        var product = new ProductDto { HasExpiry = value, IsActive = value };
        var details = await presenter.DetailsAsync(product, default);
        Assert.Equal(flag, details.Single(field => field.Label == "HasExpiry").Value);
        Assert.Equal(status, details.Single(field => field.Label == "IsActive").Value);
        var filters = await new QueryFilterPresenter(presenter).FieldsAsync(new ProductQueryModel(), default);
        foreach (var (name, expected) in new[] { ("HasExpiry", flag), ("IsActive", status) })
        {
            var options = filters.Single(field => field.Name == name).Options!;
            Assert.Equal(expected, options.Single(option => option.Value == value.ToString().ToLowerInvariant()).Label);
        }
        var related = RelatedRecordPresenter.Fields(new(Guid.NewGuid(), new Dictionary<string, object?>
        {
            ["HasExpiry"] = value,
            ["IsActive"] = value
        }));
        Assert.Equal(flag, related.Single(field => field.Label == "HasExpiry").Value);
        Assert.Equal(status, related.Single(field => field.Label == "IsActive").Value);
    }

    [Theory]
    [InlineData("ar", "تتبع الصلاحية والدفعات", "لا", "الحالة", "غير متوفر")]
    [InlineData("en", "Has Expiry", "No", "Status", "Unavailable")]
    public async Task ProductDetailsUseLocalizedExpiryAndAvailabilityLabels(string culture, string expiryLabel,
        string no, string statusLabel, string unavailable)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var product = new Product { Name = "Boolean details", HasExpiry = false, IsActive = false };
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            db.Add(product);
            await db.SaveChangesAsync();
        }
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Products/Details/{product.Id}?culture={culture}"));
        var fields = Regex.Matches(html, @"<dt>\s*(.*?)\s*</dt>\s*<dd>\s*<bdi>(.*?)</bdi>", RegexOptions.Singleline)
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);
        Assert.Equal(no, fields[expiryLabel]);
        Assert.Equal(unavailable, fields[statusLabel]);
    }

    [Theory]
    [InlineData("500.000", "500")]
    [InlineData("500.250", "500.25")]
    [InlineData("0.000", "0")]
    [InlineData("0.001", "0.001")]
    [InlineData("999999999999.999", "999999999999.999")]
    public void QuantitiesKeepTheirValueWithoutCurrencyGroupingOrTrailingZeros(string input, string expected)
    {
        var number = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, FormFieldPresenter.Quantity(number));
        Assert.Equal(expected, FormFieldPresenter.Format(number));
        foreach (var name in new[] { "Quantity", "ReservedQuantity", "AvailableQuantity", "MinStock", "MaxStock" })
        {
            Assert.Equal(expected, FormFieldPresenter.DisplayNumber(name, number));
        }
        var related = RelatedRecordPresenter.Fields(new(Guid.NewGuid(), new Dictionary<string, object?>
        {
            ["Quantity"] = number
        }));
        Assert.Equal(expected, Assert.Single(related).Value);
    }

    [Fact]
    public async Task StockGridDisplaysPlainQuantitiesAndTheCurrentProductCostWithoutChangingValuation()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            var product = new Product
            {
                Name = "Quantity display",
                CostPrice = 25
            };
            var warehouse = new Warehouse
            {
                Name = "Display warehouse",
                Code = "DISPLAY"
            };
            db.AddRange(product, warehouse);
            db.Add(new Stock
            {
                Product = product,
                Warehouse = warehouse,
                Quantity = 500.000m,
                ReservedQuantity = 12.250m,
                AverageCost = 7
            });
            await db.SaveChangesAsync();
        }
        var html = await client.GetStringAsync("/Inventory?culture=en");
        Assert.Contains("<bdi>500</bdi>", html);
        Assert.Contains("<bdi>12.25</bdi>", html);
        Assert.Contains("<bdi>487.75</bdi>", html);
        Assert.DoesNotContain("500.000", html);
        Assert.Contains("<bdi>25.00</bdi>", html);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
            (await db.Products.SingleAsync()).CostPrice = 30;
            await db.SaveChangesAsync();
            Assert.Equal(7m, (await db.Stocks.SingleAsync()).AverageCost);
        }
        html = await client.GetStringAsync("/Inventory?culture=en");
        Assert.Contains("<bdi>30.00</bdi>", html);
        Assert.DoesNotContain("<bdi>25.00</bdi>", html);
    }

    [Fact]
    public async Task HeaderProfileIsLoadedOncePerRequestAndNeverSharedAcrossRequests()
    {
        var counter = new ProfileQueryCounter();
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: services =>
            services.AddTransient<IPipelineBehavior<GetApplicationUserByIdQuery, UserDto?>>(_ => counter));
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        counter.Calls = 0;
        var html = await client.GetStringAsync("/Branches");
        Assert.Contains("sidebar-profile", html);
        Assert.Equal(1, counter.Calls);
        await client.GetStringAsync("/Branches");
        Assert.Equal(2, counter.Calls);
    }

    [Fact]
    public async Task SharedFieldsKeepInputAndFilterSemanticsAndReuseReferenceOptions()
    {
        var references = new CountingReferences();
        var fields = new FormFieldPresenter(references);
        var input = new CustomerInputDto
        {
            Name = "Customer",
            IsActive = false
        };
        var form = await fields.FormAsync(input, default);
        var name = Assert.Single(form, x => x.Name == "Name");
        Assert.True(name.Required);
        Assert.Equal(200, name.MaxLength);
        var active = Assert.Single(form, x => x.Name == "IsActive");
        Assert.Equal("checkbox", active.Type);
        Assert.Equal("false", active.Value);
        Assert.Null(active.Options);
        var query = new CustomerQueryModel();
        var filters = await new QueryFilterPresenter(fields).FieldsAsync(query, default);
        active = Assert.Single(filters, x => x.Name == "IsActive");
        Assert.Equal("select", active.Type);
        Assert.False(active.Required);
        Assert.True(string.IsNullOrEmpty(active.Value));
        Assert.Equal(new[] { "true", "false" }, active.Options!.Select(x => x.Value));
        Assert.Equal(1, references.Calls);
        var timestamp = new DateTimeOffset(2026, 11, 2, 13, 45, 0, TimeSpan.Zero);
        Assert.Equal("2026-11-02", FormFieldPresenter.Format(timestamp));
        Assert.Contains("13:45", FormFieldPresenter.Display(timestamp));
    }

    [Fact]
    public async Task ApplicationProvidesEligibleBranchesAndAllPermissionChoices()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<NEXTDbContext>();
        var current = new Branch
        {
            Name = "Current inactive",
            IsActive = false
        };
        var other = new Branch
        {
            Name = "Other inactive",
            IsActive = false
        };
        db.AddRange(current, other);
        db.AddRange(Enumerable.Range(1, 65).Select(i => new Permission
        {
            Code = "Choice." + i,
            Module = "Choices"
        }));
        await db.SaveChangesAsync();
        var sender = services.GetRequiredService<ISender>();
        var createOptions = await sender.Send(new GetAssignableBranchesQuery());
        Assert.DoesNotContain(createOptions, x => !x.IsActive);
        var editOptions = await sender.Send(new GetAssignableBranchesQuery(current.Id));
        Assert.Contains(editOptions, x => x.Id == current.Id);
        Assert.DoesNotContain(editOptions, x => x.Id == other.Id);
        var form = await services.GetRequiredService<AssignmentsPresenter>().CreateAsync("/Assignments/Create", default);
        var choices = Assert.Single(form.Fields, x => x.Name == "PermissionId").Options!;
        Assert.Equal(65, choices.Count);
        Assert.Equal(65, choices.Select(x => x.Value).Distinct().Count());
    }

    [Fact]
    public async Task SecurityFormsUseInputConstraintsAndInventoryFormsRemainSeparate()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var role = await services.GetRequiredService<RolesPresenter>().RoleFormAsync("CreateRole", "/Roles/Create");
        Assert.Equal(new[] { "Name", "Description" }, role.Fields.Select(x => x.Name));
        Assert.Equal(50, role.Fields.Single(x => x.Name == "Name").MaxLength);
        var permission = await services.GetRequiredService<PermissionsPresenter>().PermissionFormAsync("CreatePermission", "/Permissions/Create");
        Assert.Equal(120, permission.Fields.Single(x => x.Name == "Code").MaxLength);
        Assert.Equal(80, permission.Fields.Single(x => x.Name == "Module").MaxLength);
        var presenter = services.GetRequiredService<InventoryPresenter>();
        var receive = await presenter.ReceiveFormAsync("/Inventory/Receive", default);
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var transfer = await presenter.TransferFormAsync("/Inventory/Transfer", productId, warehouseId, default);
        Assert.Contains(receive.Fields, x => x.Name == "UnitCost");
        Assert.Contains(receive.Fields, x => x.Name == "ExpiryDate");
        Assert.DoesNotContain(transfer.Fields, x => x.Name is "UnitCost" or "ExpiryDate" or "WarehouseId");
        Assert.Equal(productId.ToString(), transfer.Fields.Single(x => x.Name == "ProductId").Value);
        Assert.Equal(warehouseId.ToString(), transfer.Fields.Single(x => x.Name == "SourceWarehouseId").Value);
        Assert.NotEqual(receive.Fields.Single(x => x.Name == "OperationId").Value,
            transfer.Fields.Single(x => x.Name == "OperationId").Value);
    }

    private sealed class CountingReferences : IReferenceDataService
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<ReferenceOptionDto>> GetAsync(string key, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<ReferenceOptionDto>>([new(Guid.NewGuid().ToString(), key)]);
        }
    }

    private sealed class ProfileQueryCounter : IPipelineBehavior<GetApplicationUserByIdQuery, UserDto?>
    {
        public int Calls { get; set; }
        public Task<UserDto?> Handle(GetApplicationUserByIdQuery request, RequestHandlerDelegate<UserDto?> next, CancellationToken ct)
        {
            Calls++;
            return next();
        }
    }
}
