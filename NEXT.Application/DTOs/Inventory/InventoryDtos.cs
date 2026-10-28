using System.ComponentModel.DataAnnotations;
namespace NEXT.Application.DTOs.Inventory;
public abstract class InventoryOperationDto
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    [Range(typeof(decimal), "0.001", "999999999999.999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal Quantity { get; set; }
    [StringLength(100)] public string? BatchNumber { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}
public sealed class ReceiveStockDto : InventoryOperationDto
{
    public Guid WarehouseId { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal UnitCost { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}
public sealed class TransferStockDto : InventoryOperationDto
{
    public Guid SourceWarehouseId { get; set; }
    public Guid DestinationWarehouseId { get; set; }
}
public sealed record StockDto(Guid Id, Guid ProductId, string ProductName, Guid WarehouseId, string WarehouseName,
    decimal Quantity, decimal ReservedQuantity, decimal AvailableQuantity, decimal AverageCost);
public sealed record StockMovementDto(Guid Id, string ProductName, string WarehouseName, string Type,
    decimal Quantity, decimal UnitCost, Guid? ReferenceId, string? Notes, DateTimeOffset CreatedAt);
public sealed record ProductBatchDto(Guid Id, string ProductName, string WarehouseName, string BatchNumber,
    DateOnly? ExpiryDate, decimal Quantity, decimal CostPrice);
