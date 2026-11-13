using MediatR;
using NEXT.Application.DTOs.Products;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Services.Currencies;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class ProductPresenter(ISender sender, TablePresenter tables, FormFieldPresenter fields, ExchangeRateReader exchangeRates)
    : EntityPresenter<ProductDto, ProductInputDto>(sender, tables, fields)
{
    protected override async Task<ProductInputDto> LoadEditInputAsync(Guid id, CancellationToken ct) =>
        await Sender.Send(new GetEntityByIdQuery<ProductEditDto>(id), ct);

    protected override async Task<List<FormField>> BuildFormFieldsAsync(ProductInputDto input, Guid? id, CancellationToken ct)
    {
        var form = await base.BuildFormFieldsAsync(input, id, ct);
        var rate = input.ExchangeRate ?? await exchangeRates.GetUsdRateAsync(ct);
        form.Add(new("ExchangeRate", "ExchangeRate", FormFieldPresenter.Quantity(rate), "hidden"));
        return form;
    }
}
