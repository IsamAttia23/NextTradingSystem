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
        if (rate is { } currentRate)
        {
            return await GetProductsAsync(query, currentRate, ct);
        }
        var filter = query;
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

        readMapping.ForType<Product, ProductDto>()
            .Map(destination => destination.RetailPrice,
                source => source.CostPrice * currentRate * (1 + source.RetailPrice / 100m))
            .Map(destination => destination.WholesalePrice,
                source => source.CostPrice * currentRate * (1 + source.WholesalePrice / 100m))
            .Map(destination => destination.VipPrice,
                source => source.CostPrice * currentRate * (1 + source.VipPrice / 100m));
        return readMapping;
    }

    private async Task<TPaginationQueryModel<TDto>> GetProductsAsync(PaginationQuery query, decimal rate, CancellationToken ct)
    {
        var requested = query as ProductQueryModel ?? new ProductQueryModel();
        var filter = requested.Adapt(new ProductQueryModel(), mapping);
        filter.SearchTerm = query.SearchTerm;
        filter.MinCostPrice = filter.MaxCostPrice = null;
        filter.MinRetailPrice = filter.MaxRetailPrice = null;
        filter.MinWholesalePrice = filter.MaxWholesalePrice = null;
        filter.MinVipPrice = filter.MaxVipPrice = null;

        var entities = repository.FindAsync(repository.BuildFilterExpression(filter), ct);
        var ordered = repository.ApplySorting(entities, query.SortBy, query.SortDirection);
        var projected = ordered.ProjectToType<TDto>(CreateReadMapping(rate));
        var products = (IQueryable<ProductDto>)(object)projected;
        products = products.Where(product =>
            (!requested.MinCostPrice.HasValue || product.CostPrice >= requested.MinCostPrice.Value) &&
            (!requested.MaxCostPrice.HasValue || product.CostPrice <= requested.MaxCostPrice.Value) &&
            (!requested.MinRetailPrice.HasValue || product.RetailPrice >= requested.MinRetailPrice.Value) &&
            (!requested.MaxRetailPrice.HasValue || product.RetailPrice <= requested.MaxRetailPrice.Value) &&
            (!requested.MinWholesalePrice.HasValue || product.WholesalePrice >= requested.MinWholesalePrice.Value) &&
            (!requested.MaxWholesalePrice.HasValue || product.WholesalePrice <= requested.MaxWholesalePrice.Value) &&
            (!requested.MinVipPrice.HasValue || product.VipPrice >= requested.MinVipPrice.Value) &&
            (!requested.MaxVipPrice.HasValue || product.VipPrice <= requested.MaxVipPrice.Value));

        System.Linq.Expressions.Expression<Func<ProductDto, decimal>>? price = query.SortBy?.Trim().ToLowerInvariant() switch
        {
            "retailprice" => product => product.RetailPrice,
            "wholesaleprice" => product => product.WholesalePrice,
            "vipprice" => product => product.VipPrice,
            _ => null
        };
        if (price is not null)
        {
            products = query.SortDirection == NEXT.Domain.Enums.SortDirection.Descending
                ? products.OrderByDescending(price).ThenByDescending(product => product.Id)
                : products.OrderBy(price).ThenBy(product => product.Id);
        }

        return await pagination.GetPagedList((IQueryable<TDto>)(object)products, query, ct);
    }
}
