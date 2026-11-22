using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Persistence;

public interface IInvoiceConfirmationStore
{
    Task<Invoice?> LockInvoiceAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Stock>> LockStocksAsync(Guid warehouseId, Guid[] productIds, CancellationToken ct);
    Task<IReadOnlyList<ProductBatch>> LockBatchesAsync(Guid warehouseId, Guid[] productIds, DateOnly today, CancellationToken ct);
}
