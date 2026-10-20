using NEXT.Domain.Common;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class PurchaseOrder : AuditableEntity<Guid>
{
    public string Number { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? ExpectedDate { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
