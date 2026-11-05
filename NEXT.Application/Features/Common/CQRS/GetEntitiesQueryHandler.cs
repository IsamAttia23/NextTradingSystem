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

public sealed class GetEntitiesQueryHandler<TEntity, TDto>(
    IKeyRepository<TEntity, Guid> repository, IPaginationServices pagination,
    IEntityValidator<PaginationQuery> validator, TypeAdapterConfig mapping, ExchangeRateReader? exchangeRates = null)
    : IRequestHandler<GetEntitiesQuery<TDto>, TPaginationQueryModel<TDto>>
    where TEntity : AuditableEntity<Guid> where TDto : class
{
    public async Task<TPaginationQueryModel<TDto>> Handle(GetEntitiesQuery<TDto> request, CancellationToken ct)
    {
        var query = request.Pagination;
        if (query is null)
        {
            RequestValidation.Fail("Pagination", "Query is required.");
        }
        validator.Validate(query);
        var rate = await GetProductRateAsync(ct);
        var filter = CreateFilter(query, rate);
        var readMapping = CreateReadMapping(rate);
        var expression = repository.BuildFilterExpression(filter);
        var entities = repository.FindAsync(expression, ct);
        var orderedEntities = repository.ApplySorting(entities, query.SortBy, query.SortDirection);
        var pagedList = await pagination.GetPagedList(orderedEntities,
            source => source.ProjectToType<TDto>(readMapping), query, ct);
        return pagedList;
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

    private TypeAdapterConfig CreateReadMapping(decimal? rate)
    {
        var configuration = mapping;
        if (rate is not { } currentRate)
        {
            return configuration;
        }
        var readMapping = configuration.Clone();
        readMapping.ForType<Product, ProductDto>()
            .Map(destination => destination.CostPrice, source => source.CostPrice * currentRate)
            .Map(destination => destination.ExchangeRate, source => (decimal?)currentRate);
        return readMapping;
    }

    private PaginationQuery CreateFilter(PaginationQuery query, decimal? rate)
    {
        if (query is not ProductQueryModel productQuery || rate is not { } currentRate)
        {
            return query;
        }
        var filter = productQuery.Adapt(new ProductQueryModel(), mapping);
        filter.MinCostPrice = productQuery.MinCostPrice / currentRate;
        filter.MaxCostPrice = productQuery.MaxCostPrice / currentRate;
        return filter;
    }
}
