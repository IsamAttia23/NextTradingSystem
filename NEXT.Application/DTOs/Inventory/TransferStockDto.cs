namespace NEXT.Application.DTOs.Inventory;

public sealed class TransferStockDto : InventoryOperationDto
{
    public Guid SourceWarehouseId { get; set; }
    public Guid DestinationWarehouseId { get; set; }
}
