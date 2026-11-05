using NEXT.Application.DTOs.Products;
using NEXT.Application.QueryModels;
using NEXT.Application.Services.Currencies;
using NEXT.Domain.Entities;
using NEXT.Application.Validation;
using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Exceptions;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.Common.CQRS;

public sealed class GetEntityByIdQueryHandler<TEntity, TDto>(
    IKeyRepository<TEntity, Guid> repository, IQueryExecutor executor, TypeAdapterConfig? mapping = null, ExchangeRateReader? exchangeRates = null)
    : IRequestHandler<GetEntityByIdQuery<TDto>, TDto>
    where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task<TDto> Handle(GetEntityByIdQuery<TDto> request, CancellationToken ct)
    {
        RequestValidation.Identifier(request.Id);
        var rate = await GetProductRateAsync(ct);
        var readMapping = CreateReadMapping(rate, request.ForEdit);
        var entities = repository.FindAsync(x => x.Id == request.Id, ct);
        return await executor.FirstOrDefaultAsync(entities.ProjectToType<TDto>(readMapping), ct)
            ?? throw new NotFoundException("Record was not found.");
    }

    private async Task<decimal?> GetProductRateAsync(CancellationToken ct)
    {
        if (typeof(TEntity) != typeof(Product) || typeof(TDto) != typeof(ProductDto))
        {
            return null;
        }
        var reader = exchangeRates ?? throw new InvalidOperationException("Exchange rate reader is not registered.");
        return await reader.GetUsdRateAsync(ct);
    }

    private TypeAdapterConfig CreateReadMapping(decimal? rate, bool forEdit)
    {
        var configuration = (mapping ?? new TypeAdapterConfig());
        if (rate is not { } currentRate)
        {
            return configuration;
        }
        var readMapping = configuration.Clone();
        readMapping.ForType<Product, ProductDto>()
            .Map(destination => destination.CostPrice, source => source.CostPrice * currentRate)
            .Map(destination => destination.ExchangeRate, source => (decimal?)currentRate);

        if (forEdit)
        {
            return readMapping;
        }

        readMapping.ForType<Product, ProductDto>()
            .Map(destination => destination.RetailPrice,
                source => source.CostPrice * currentRate * (1 + source.RetailPrice / 100m))
            .Map(destination => destination.WholesalePrice,
                source => source.CostPrice * currentRate * (1 + source.WholesalePrice / 100m))
            .Map(destination => destination.VipPrice,
                source => source.CostPrice * currentRate * (1 + source.VipPrice / 100m));
        return readMapping;
    }
}
