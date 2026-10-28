using NEXT.Application.Services.Catalog;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Inventory;
using NEXT.Domain.Entities;
namespace NEXT.Application.Services.Inventory;
public sealed class InventoryQueryService(IRepository<Stock> stocks, IRepository<StockMovement> movements,
    IRepository<ProductBatch> batches, IPaginationServices pagination) : IInventoryQueryService
{
    public Task<TPaginationQueryModel<StockDto>> StocksAsync(PaginationQuery query, CancellationToken ct = default) =>
        pagination.GetPaggedList(CatalogQueryPipeline.Apply(stocks.Query(), query).Select(x => new StockDto(x.Id, x.ProductId, x.Product.Name,
            x.WarehouseId, x.Warehouse.Name, x.Quantity, x.ReservedQuantity, x.Quantity - x.ReservedQuantity, x.AverageCost)), query, ct);
    public Task<TPaginationQueryModel<StockMovementDto>> MovementsAsync(PaginationQuery query, CancellationToken ct = default) =>
        pagination.GetPaggedList(CatalogQueryPipeline.Apply(movements.Query(), query).Select(x => new StockMovementDto(x.Id, x.Product.Name,
            x.Warehouse.Name, x.Type.ToString(), x.Quantity, x.UnitCost, x.ReferenceId, x.Notes, x.CreatedAt)), query, ct);
    public Task<TPaginationQueryModel<ProductBatchDto>> BatchesAsync(PaginationQuery query, CancellationToken ct = default) =>
        pagination.GetPaggedList(CatalogQueryPipeline.Apply(batches.Query(), query, "ExpiryDate").Select(x => new ProductBatchDto(x.Id, x.Product.Name,
            x.Warehouse.Name, x.BatchNumber, x.ExpiryDate, x.Quantity, x.CostPrice)), query, ct);
}
