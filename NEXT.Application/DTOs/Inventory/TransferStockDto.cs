namespace NEXT.Application.DTOs.Inventory;

public sealed class TransferStockDto : InventoryOperationDto
{
    [NEXT.Application.Validation.NotEmptyGuid] public Guid SourceWarehouseId { get; set; }
    [NEXT.Application.Validation.NotEmptyGuid] public Guid DestinationWarehouseId { get; set; }
}
