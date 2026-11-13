using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Products;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.QueryModels;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Products;

public sealed class ProductQueries(IReadRepository<Product> repository, IQueryExecutor executor,
    IPaginationServices pagination, IEntityValidator<PaginationQuery> validator, ProductPricing pricing, TypeAdapterConfig mapping)
    : IRequestHandler<GetEntitiesQuery<ProductDto>, TPaginationQueryModel<ProductDto>>,
      IRequestHandler<GetEntityByIdQuery<ProductDto>, ProductDto>,
      IRequestHandler<GetEntityByIdQuery<ProductEditDto>, ProductEditDto>
{
    public Task<ProductDto> Handle(GetEntityByIdQuery<ProductDto> request, CancellationToken ct) =>
        ReadAsync<ProductDto>(request.Id, pricing.ReadMapping, ct);

    public Task<ProductEditDto> Handle(GetEntityByIdQuery<ProductEditDto> request, CancellationToken ct) =>
        ReadAsync<ProductEditDto>(request.Id, pricing.EditMapping, ct);

    private async Task<TDto> ReadAsync<TDto>(Guid id, Func<decimal, TypeAdapterConfig> configure, CancellationToken ct)
        where TDto : class
    {
        RequestValidation.Identifier(id);
        var rate = await pricing.GetRateAsync(ct);
        return await GetEntityByIdQueryHandler<Product, TDto>.ReadAsync(repository, executor, id, configure(rate), ct);
    }

    public async Task<TPaginationQueryModel<ProductDto>> Handle(GetEntitiesQuery<ProductDto> request, CancellationToken ct)
    {
        var query = request.Pagination;
        if (query is null)
        {
            RequestValidation.Fail("Pagination", "Query is required.");
        }
        validator.Validate(query);
        var rate = await pricing.GetRateAsync(ct);
        var requested = query as ProductQueryModel ?? new ProductQueryModel();
        var filter = requested.Adapt(new ProductQueryModel(), mapping);
        filter.SearchTerm = query.SearchTerm;
        filter.MinCostPrice = filter.MaxCostPrice = null;
        filter.MinRetailPrice = filter.MaxRetailPrice = null;
        filter.MinWholesalePrice = filter.MaxWholesalePrice = null;
        filter.MinVipPrice = filter.MaxVipPrice = null;

        var entities = repository.Query(repository.BuildFilterExpression(filter));
        var ordered = repository.ApplySorting(entities, query.SortBy, query.SortDirection);
        var projected = ordered.ProjectToType<ProductDto>(pricing.ReadMapping(rate));
        var products = projected.Where(product =>
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

        return await pagination.GetPagedList(products, query, ct);
    }
}
