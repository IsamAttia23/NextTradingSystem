using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Inventory;

public sealed class ReceiveStockDto : InventoryOperationDto
{
    [NEXT.Application.Validation.NotEmptyGuid]
    public Guid WarehouseId { get; set; }

    public bool ReceiveInPackages { get; set; }

    public DateOnly? ExpiryDate { get; set; }
}
