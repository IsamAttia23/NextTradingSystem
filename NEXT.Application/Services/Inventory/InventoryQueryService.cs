using NEXT.Application.Validation;
using System.Linq.Expressions;
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
    IProductBatchRepository batches, IPaginationServices pagination, IEntityValidator<PaginationQuery> validator) : IInventoryQueryService
{
    public Task<TPaginationQueryModel<StockDto>> StocksAsync(PaginationQuery query, CancellationToken ct = default) =>
        GetPagedList(stocks, query, x => new StockDto(x.Id, x.ProductId, x.Product.Name,
            x.WarehouseId, x.Warehouse.Name, x.Quantity, x.ReservedQuantity, x.Quantity - x.ReservedQuantity, x.AverageCost), ct);
    public Task<TPaginationQueryModel<StockMovementDto>> MovementsAsync(PaginationQuery query, CancellationToken ct = default) =>
        GetPagedList(movements, query, x => new StockMovementDto(x.Id, x.Product.Name,
            x.Warehouse.Name, x.Type.ToString(), x.Quantity, x.UnitCost, x.ReferenceId, x.Notes, x.CreatedAt), ct);
    public Task<TPaginationQueryModel<ProductBatchDto>> BatchesAsync(PaginationQuery query, CancellationToken ct = default) =>
        GetPagedList(batches, query, x => new ProductBatchDto(x.Id, x.Product.Name,
            x.Warehouse.Name, x.BatchNumber, x.ExpiryDate, x.Quantity, x.CostPrice), ct);

    private async Task<TPaginationQueryModel<TDto>> GetPagedList<TEntity, TDto>(
        IRepository<TEntity> repository, PaginationQuery query, Expression<Func<TEntity, TDto>> selector, CancellationToken ct)
        where TEntity : class where TDto : class
    {
        if (query is null) RequestValidation.Fail("Query", "Query is required.");
        validator.Validate(query);
        var expression = repository.BuildFilterExpression(query);
        var entities = repository.FindAsync(expression, ct);
        var orderedEntities = repository.ApplySorting(entities, query.SortBy, query.SortDirection);
        var pagedList = await pagination.GetPagedList(orderedEntities,
            source => source.Select(selector), query, ct);
        return pagedList;
    }
}
