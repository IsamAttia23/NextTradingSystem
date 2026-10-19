using NEXT.Domain.Common;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class Payment : AuditableEntity<Guid>
{
    public string PaymentNumber { get; set; } = string.Empty;
    public PaymentType PaymentType { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid? SalesInvoiceId { get; set; }
    public SalesInvoice? SalesInvoice { get; set; }
    public Guid? PurchaseInvoiceId { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? Notes { get; set; }
}
