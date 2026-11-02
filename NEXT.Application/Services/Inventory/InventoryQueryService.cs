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
    TypeAdapterConfig mapping) : IInventoryQueryService
{
    public Task<TPaginationQueryModel<StockDto>> StocksAsync(PaginationQuery query, CancellationToken ct = default) =>
        GetPagedList<Stock, StockDto>(stocks, query, ct);
    public Task<TPaginationQueryModel<StockMovementDto>> MovementsAsync(PaginationQuery query, CancellationToken ct = default) =>
        GetPagedList<StockMovement, StockMovementDto>(movements, query, ct);
    public Task<TPaginationQueryModel<ProductBatchDto>> BatchesAsync(PaginationQuery query, CancellationToken ct = default) =>
        GetPagedList<ProductBatch, ProductBatchDto>(batches, query, ct);

    private async Task<TPaginationQueryModel<TDto>> GetPagedList<TEntity, TDto>(
        IRepository<TEntity> repository, PaginationQuery query, CancellationToken ct)
        where TEntity : class where TDto : class
    {
        if (query is null) RequestValidation.Fail("Query", "Query is required.");
        validator.Validate(query);
        var expression = repository.BuildFilterExpression(query);
        var entities = repository.FindAsync(expression, ct);
        var orderedEntities = repository.ApplySorting(entities, query.SortBy, query.SortDirection);
        return await pagination.GetPagedList(orderedEntities,
            source => source.ProjectToType<TDto>(mapping), query, ct);
    }
}
