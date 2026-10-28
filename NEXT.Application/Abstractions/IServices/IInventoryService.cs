using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Inventory;
namespace NEXT.Application.Abstractions.IServices;
public interface IInventoryService
{
    Task ReceiveAsync(ReceiveStockDto input, CancellationToken ct = default);
    Task TransferAsync(TransferStockDto input, CancellationToken ct = default);
}
public interface IInventoryQueryService
{
    Task<TPaginationQueryModel<StockDto>> StocksAsync(PaginationQuery query, CancellationToken ct = default);
    Task<TPaginationQueryModel<StockMovementDto>> MovementsAsync(PaginationQuery query, CancellationToken ct = default);
    Task<TPaginationQueryModel<ProductBatchDto>> BatchesAsync(PaginationQuery query, CancellationToken ct = default);
}
