using NEXT.Application.DTOs.Inventory;
namespace NEXT.Application.Abstractions.IServices;
public interface IInventoryService
{
    Task ReceiveAsync(ReceiveStockDto input, CancellationToken ct = default);
    Task TransferAsync(TransferStockDto input, CancellationToken ct = default);
}
