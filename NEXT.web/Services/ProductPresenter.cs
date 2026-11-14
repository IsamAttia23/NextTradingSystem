using MediatR;
using System.Reflection;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.DTOs.Products;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Services.Currencies;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class ProductPresenter(ISender sender, TablePresenter tables, FormFieldPresenter fields, ExchangeRateReader exchangeRates)
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
        return base.DisplayCell(item, column);
    }

    protected override TableRow CustomizeRow(ProductDto item, TableRow row)
    {
        var actions = row.Actions;
        if (item.IsActive)
        {
            actions = [new("Receive", $"/Inventory/Receive?productId={item.Id}"), ..actions];
        }
        return row with { Actions = actions, StockLevel = StockLevel(item) };
    }

    protected override async Task<IReadOnlyList<DetailFieldDto>> BuildDetailsAsync(ProductDto item, CancellationToken ct)
    {
        var details = await base.BuildDetailsAsync(item, ct);
        return [..details.Where(field => field.Label != nameof(ProductDto.IsActive)),
            new("Status", Availability(item), TranslateValue: true)];
    }

    private static string Availability(ProductDto item)
    {
        if (!item.IsActive || item.AvailableQuantity <= 0)
        {
            return "ProductUnavailable";
        }
        return "ProductAvailable";
    }

    private static string StockLevel(ProductDto item)
    {
        if (item.AvailableQuantity <= 0)
        {
            return "empty";
        }
        if (item.AvailableQuantity <= item.MinStock)
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
            if (id is null && field.Name is nameof(ProductInputDto.CostPrice) or nameof(ProductInputDto.RetailPrice)
                or nameof(ProductInputDto.WholesalePrice) or nameof(ProductInputDto.VipPrice))
            {
                form[index] = field with { Value = null };
            }
            if (field.Name == nameof(ProductInputDto.IsActive))
            {
                form[index] = field with { Label = "ProductEnabled" };
            }
        }
        var rate = input.ExchangeRate ?? await exchangeRates.GetUsdRateAsync(ct);
        form.Add(new("ExchangeRate", "ExchangeRate", FormFieldPresenter.Quantity(rate), "hidden"));
        return form;
    }
}
