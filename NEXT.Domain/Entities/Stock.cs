using NEXT.Domain.Common;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class Stock : AuditableEntity<Guid>
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableQuantity => Quantity - ReservedQuantity;
    public decimal AverageCost { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; }
}
