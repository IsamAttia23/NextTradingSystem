using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Inventory;
namespace NEXT.Application.Abstractions.IServices;

public interface IInventoryQueryService
{
    Task<TPaginationQueryModel<StockDto>> StocksAsync(PaginationQuery query, CancellationToken ct = default);
    Task<TPaginationQueryModel<StockMovementDto>> MovementsAsync(PaginationQuery query, CancellationToken ct = default);
    Task<TPaginationQueryModel<ProductBatchDto>> BatchesAsync(PaginationQuery query, CancellationToken ct = default);
}
