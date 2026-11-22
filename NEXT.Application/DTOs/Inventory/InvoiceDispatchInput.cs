namespace NEXT.Application.DTOs.Inventory;

public sealed record InvoiceDispatchInput(Guid InvoiceId, Guid WarehouseId,
    IReadOnlyDictionary<Guid, decimal> Quantities, DateTimeOffset ActionAt);
