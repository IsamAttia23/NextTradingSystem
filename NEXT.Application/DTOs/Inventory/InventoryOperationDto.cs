using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Inventory;

public abstract class InventoryOperationDto
{
    [NEXT.Application.Validation.NotEmptyGuid] public Guid OperationId { get; set; } = Guid.NewGuid();
    [NEXT.Application.Validation.NotEmptyGuid] public Guid ProductId { get; set; }
    [Range(typeof(decimal), "0.001", "999999999999.999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal Quantity { get; set; }
    [StringLength(100)] public string? BatchNumber { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}
