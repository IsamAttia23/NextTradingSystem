using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class InvoiceItem : AuditableEntity<Guid>
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public Guid? ProductId { get; set; }
    public string? ManualName { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
}
