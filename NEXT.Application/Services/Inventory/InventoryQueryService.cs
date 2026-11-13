using NEXT.Application.Services.Currencies;
using NEXT.Application.Validation;
using Mapster;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Inventory;
using NEXT.Domain.Entities;

namespace NEXT.Application.Services.Inventory;

public sealed class InventoryQueryService(IStockRepository stocks, IStockMovementRepository movements,
    IProductBatchRepository batches, IPaginationServices pagination, IEntityValidator<PaginationQuery> validator,
    TypeAdapterConfig mapping, ExchangeRateReader? exchangeRates = null) : IInventoryQueryService
{
    public Task<TPaginationQueryModel<StockDto>> StocksAsync(PaginationQuery query, CancellationToken ct = default) =>
        GetPagedList<Stock, StockDto>(stocks, query, ct);
    public Task<TPaginationQueryModel<StockMovementDto>> MovementsAsync(PaginationQuery query, CancellationToken ct = default) =>
        GetPagedList<StockMovement, StockMovementDto>(movements, query, ct);
    public Task<TPaginationQueryModel<ProductBatchDto>> BatchesAsync(PaginationQuery query, CancellationToken ct = default) =>
        GetPagedList<ProductBatch, ProductBatchDto>(batches, query, ct);

    private async Task<TPaginationQueryModel<TDto>> GetPagedList<TEntity, TDto>(
        IReadRepository<TEntity> repository, PaginationQuery query, CancellationToken ct)
        where TEntity : class where TDto : class
    {
        if (query is null)
        {
            RequestValidation.Fail("Query", "Query is required.");
        }
        validator.Validate(query);
        var expression = repository.BuildFilterExpression(query);
        var entities = repository.Query(expression);
        var orderedEntities = repository.ApplySorting(entities, query.SortBy, query.SortDirection);
        var readMapping = await CreateReadMappingAsync<TDto>(ct);
        return await pagination.GetPagedList(orderedEntities,
            source => source.ProjectToType<TDto>(readMapping), query, ct);
    }

    private async Task<TypeAdapterConfig> CreateReadMappingAsync<TDto>(CancellationToken ct)
    {
        if (typeof(TDto) != typeof(StockDto))
        {
            return mapping;
        }
        var reader = exchangeRates ?? throw new InvalidOperationException("Exchange rate reader is not registered.");
        var rate = await reader.GetUsdRateAsync(ct);
        var readMapping = mapping.Clone();
        NEXT.Application.Features.Common.Mapping.RecordMappingRegister.ConfigureStockMapping(
            readMapping, stock => stock.Product.CostPrice * rate);
        return readMapping;
    }
}
