using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class PurchaseInvoiceItem : AuditableEntity<Guid>
{
    public Guid PurchaseInvoiceId { get; set; }
    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid? ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }
}
