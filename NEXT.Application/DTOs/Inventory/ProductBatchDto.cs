namespace NEXT.Application.DTOs.Inventory;

public sealed record ProductBatchDto(Guid Id, string ProductName, string WarehouseName, string BatchNumber,
    DateOnly? ExpiryDate, decimal Quantity, decimal CostPrice, int? UnitsPerPackage = null, string? BaseUnitName = null, string? PackagingUnitName = null);
