using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class Invoice : AuditableEntity<Guid>
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Customer? Customer { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public Guid UserId { get; set; }
    public string? CreatedByFullName { get; set; }
    public UserAccount User { get; set; } = null!;
    public DateTimeOffset InvoiceDate { get; set; } = DateTimeOffset.UtcNow;
    public InvoiceType Type { get; set; } = InvoiceType.Sales;
    public int? LegacyInvoiceTypeCode { get; set; }
    public decimal SubTotal { get; set; }
    public DiscountType? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public PaymentMethod? PaymentMethod { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public DateTimeOffset? LastActionAt { get; set; }
    public string? Notes { get; set; }
    public Guid? ParentInvoiceId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Invoice? ParentInvoice { get; set; }
    public ICollection<Invoice> Returns { get; set; } = new List<Invoice>();
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}
