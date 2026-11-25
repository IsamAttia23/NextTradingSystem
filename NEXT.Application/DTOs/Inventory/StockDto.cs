namespace NEXT.Application.DTOs.Inventory;

public sealed record StockDto(Guid Id, Guid ProductId, string ProductName, Guid WarehouseId, string WarehouseName,
    decimal Quantity, decimal ReservedQuantity, decimal AvailableQuantity, decimal AverageCost, decimal CostPrice,
    int? UnitsPerPackage = null, string? BaseUnitName = null, string? PackagingUnitName = null, decimal TotalCost = 0);
