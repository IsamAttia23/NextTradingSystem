using MediatR;
using System.Reflection;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.DTOs.Products;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Services.Currencies;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class ProductPresenter(ISender sender, TablePresenter tables, FormFieldPresenter fields, ExchangeRateReader exchangeRates, Microsoft.Extensions.Localization.IStringLocalizer<NEXT.web.Localization.UiText> text, IHttpContextAccessor context)
    : EntityPresenter<ProductDto, ProductInputDto>(sender, tables, fields)
{
    protected override IReadOnlyList<string> TableColumns =>
        [nameof(ProductDto.Name), nameof(ProductDto.AvailableQuantity), nameof(ProductDto.MinStock), nameof(ProductDto.IsActive)];

    protected override string DisplayCell(ProductDto item, PropertyInfo column)
    {
        if (column.Name == nameof(ProductDto.IsActive))
        {
            return Availability(item);
        }
        if (column.Name == nameof(ProductDto.AvailableQuantity))
        {
            return InventoryQuantityDisplay.Format(item.AvailableQuantity, item.UnitsPerPackage,
                item.BaseUnitName, item.PackagingUnitName, text);
        }
        return base.DisplayCell(item, column);
    }

    protected override TableRow CustomizeRow(ProductDto item, TableRow row)
    {
        var actions = row.Actions;
        if (item.IsActive)
        {
            actions = [new("Receive", (context.HttpContext?.Request.PathBase.Value ?? "") + $"/Inventory/Receive?productId={item.Id}"), ..actions];
        }
        return row with { Actions = actions, StockLevel = StockLevel(item) };
    }

    protected override async Task<IReadOnlyList<DetailFieldDto>> BuildDetailsAsync(ProductDto item, CancellationToken ct)
    {
        var details = await base.BuildDetailsAsync(item, ct);
        var presented = details.Select(field => FormatProductDetail(item, field));
        return [..presented.Where(field => field.Label != nameof(ProductDto.IsActive)),
            new("Status", Availability(item), TranslateValue: true)];
    }

    private DetailFieldDto FormatProductDetail(ProductDto item, DetailFieldDto field)
    {
        if (field.Label is nameof(ProductDto.AvailableQuantity))
        {
            return field with { Value = InventoryQuantityDisplay.Format(item.AvailableQuantity,
                item.UnitsPerPackage, item.BaseUnitName, item.PackagingUnitName, text) };
        }
        if (item.UnitsPerPackage is not > 0 || item.PackagingUnitName is null || item.BaseUnitName is null)
        {
            return field;
        }
        decimal? amount = field.Label switch
        {
            nameof(ProductDto.CostPrice) => item.CostPrice,
            nameof(ProductDto.RetailPrice) => item.RetailPrice,
            nameof(ProductDto.WholesalePrice) => item.WholesalePrice,
            nameof(ProductDto.VipPrice) => item.VipPrice,
            _ => null
        };
        if (amount is not { } price)
        {
            return field;
        }
        var unitPrice = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        if (unitPrice > decimal.MaxValue / item.UnitsPerPackage.Value)
        {
            return field;
        }
        return field with { Value = text["PackagedPrice", unitPrice.ToString("N2"), item.BaseUnitName,
            (unitPrice * item.UnitsPerPackage.Value).ToString("N2"), item.PackagingUnitName] };
    }

    private static string Availability(ProductDto item)
    {
        if (!item.IsActive || item.AvailableQuantity <= 0)
        {
            return "ProductUnavailable";
        }
        return StockLevel(item) == "low" ? "ProductLowStock" : "ProductAvailable";
    }

    private static string StockLevel(ProductDto item)
    {
        if (item.AvailableQuantity <= 0)
        {
            return "empty";
        }
        if (item.IsActive && item.AvailableQuantity <= item.MinStock)
        {
            return "low";
        }
        return "normal";
    }

    protected override async Task<ProductInputDto> LoadEditInputAsync(Guid id, CancellationToken ct) =>
        await Sender.Send(new GetEntityByIdQuery<ProductEditDto>(id), ct);

    protected override async Task<List<FormField>> BuildFormFieldsAsync(ProductInputDto input, Guid? id, CancellationToken ct)
    {
        var form = await base.BuildFormFieldsAsync(input, id, ct);
        for (var index = 0; index < form.Count; index++)
        {
            var field = form[index];
            field = field with { Group = ProductFieldGroup(field.Name) };
            if (id is null && field.Name is nameof(ProductInputDto.CostPrice) or nameof(ProductInputDto.RetailPrice)
                or nameof(ProductInputDto.WholesalePrice) or nameof(ProductInputDto.VipPrice))
            {
                field = field with { Value = null };
            }
            if (field.Name == nameof(ProductInputDto.CostPrice))
            {
                field = field with { Label = "BaseUnitCost" };
            }
            if (field.Name == nameof(ProductInputDto.IsActive))
            {
                field = field with { Label = "ProductEnabled" };
            }
            form[index] = field;
        }
        if (id is null)
        {
            const string opening = "OpeningStock";
            form.Add(new("AddOpeningStock", "AddOpeningStock", "false", "checkbox", Group: opening));
            form.Add(new("OpeningWarehouseId", "Warehouse", Type: "select",
                Options: await Fields.OptionsAsync("Warehouses", ct), Group: opening));
            form.Add(new("OpeningInPackages", "QuantityUnit", "false", "select",
                Options: InventoryQuantityDisplay.UnitOptions, Group: opening));
            form.Add(new("OpeningQuantity", "Quantity", Type: "number", Group: opening));
            form.Add(new("OpeningQuantityPreview", "ReceiptQuantityPreview", Type: "quantity-output", Group: opening));
            form.Add(new("OpeningBatchNumber", "BatchNumber", MaxLength: 100, Group: opening));
            form.Add(new("OpeningExpiryDate", "ExpiryDate", Type: "date", Group: opening));
        }
        var rate = input.ExchangeRate ?? await exchangeRates.GetUsdRateAsync(ct);
        form.Add(new("ExchangeRate", "ExchangeRate", FormFieldPresenter.Quantity(rate), "hidden"));
        return form;
    }

    private static string ProductFieldGroup(string name) => name switch
    {
        "UnitOfMeasureId" or "PackagingUnitId" or "UnitsPerPackage" => "ProductUnits",
        "CostPrice" or "RetailPrice" or "WholesalePrice" or "VipPrice" or "TaxRate" => "ProductPricing",
        "MinStock" or "MaxStock" or "HasExpiry" or "IsActive" => "ProductInventory",
        _ => "ProductInformation"
    };
}
