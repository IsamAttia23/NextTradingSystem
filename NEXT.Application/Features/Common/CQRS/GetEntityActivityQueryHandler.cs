using NEXT.Application.Validation;
using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Exceptions;
using NEXT.Domain.Common.Entities;
using NEXT.Application.Services.Currencies;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Common.CQRS;

public sealed class GetEntityActivityQueryHandler<TEntity, TDto>(IKeyRepository<TEntity, Guid> repository,
    IRelatedRecordsReader related, ExchangeRateReader? exchangeRates = null)
    : IRequestHandler<GetEntityActivityQuery<TDto>, ActivityPageDto> where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task<ActivityPageDto> Handle(GetEntityActivityQuery<TDto> request, CancellationToken ct)
    {
        RequestValidation.Identifier(request.Id);
        _ = await repository.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Record was not found.");
        var page = await related.ReadAsync<TEntity>(request.Id, request.Kind, request.Page, ct);
        return await ConvertProductCostsAsync(page, ct);
    }

    private async Task<ActivityPageDto> ConvertProductCostsAsync(ActivityPageDto page, CancellationToken ct)
    {
        if (page.Selected != nameof(Product) || page.Records.Count == 0)
        {
            return page;
        }
        var reader = exchangeRates ?? throw new InvalidOperationException("Exchange rate reader is not registered.");
        var rate = await reader.GetUsdRateAsync(ct);
        var records = page.Records.Select(record =>
        {
            var values = record.Values.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (values.TryGetValue(nameof(Product.CostPrice), out var value) && value is decimal cost)
            {
                values[nameof(Product.CostPrice)] = cost * rate;
            }
            return record with { Values = values };
        }).ToArray();
        return page with { Records = records };
    }
}
