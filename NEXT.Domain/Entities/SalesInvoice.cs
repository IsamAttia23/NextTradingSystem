using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class SalesInvoice : AuditableEntity<Guid>
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public DateTimeOffset InvoiceDate { get; set; } = DateTimeOffset.UtcNow;
    public InvoiceType InvoiceType { get; set; } = InvoiceType.Sale;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public PaymentMethod? PaymentMethod { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? Notes { get; set; }
    public Guid? ParentInvoiceId { get; set; }
    public byte[] RowVersion { get; set; }

    public SalesInvoice? ParentInvoice { get; set; }
    public ICollection<SalesInvoice> Returns { get; set; } = new List<SalesInvoice>();
    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}
