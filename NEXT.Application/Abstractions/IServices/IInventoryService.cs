using NEXT.Application.DTOs.Inventory;

namespace NEXT.Application.Abstractions.IServices;

public interface IInventoryService
{
    Task ReceiveAsync(ReceiveStockDto input, CancellationToken ct = default);
    Task TransferAsync(TransferStockDto input, CancellationToken ct = default);
    /// <summary>Issues reserved quantities within the caller's transaction and invoice lock.</summary>
    Task IssueReservedInvoiceAsync(InvoiceDispatchInput input, CancellationToken ct = default);
}
