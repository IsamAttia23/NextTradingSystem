using System.ComponentModel.DataAnnotations;
namespace NEXT.Application.DTOs.Inventory;

public sealed class ReceiveStockDto : InventoryOperationDto
{
    public Guid WarehouseId { get; set; }

    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal UnitCost { get; set; }

    public DateOnly? ExpiryDate { get; set; }
}
