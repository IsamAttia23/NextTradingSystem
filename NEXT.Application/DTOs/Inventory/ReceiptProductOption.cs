namespace NEXT.Application.DTOs.Inventory;

public sealed record ReceiptProductOption(Guid Id, string Name, string? BaseUnit, string? PackagingUnit, int? UnitsPerPackage);
