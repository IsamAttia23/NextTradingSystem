namespace NEXT.Application.DTOs.Inventory;

public sealed record StockMovementDto(Guid Id, string ProductName, string WarehouseName, string Type,
    decimal Quantity, decimal UnitCost, Guid? ReferenceId, string? Notes, DateTimeOffset CreatedAt);
