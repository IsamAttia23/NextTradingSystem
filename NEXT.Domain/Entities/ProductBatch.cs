using NEXT.Domain.Common;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class ProductBatch : AuditableEntity<Guid>
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly? ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public decimal CostPrice { get; set; }
}
