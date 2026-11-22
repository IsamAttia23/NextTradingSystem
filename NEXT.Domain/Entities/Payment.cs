using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class Payment : AuditableEntity<Guid>
{
    public string PaymentNumber { get; set; } = string.Empty;
    public string? RecordedByFullName { get; set; }
    public PaymentType PaymentType { get; set; }
    public PaymentPurpose Purpose { get; set; }
    public Guid? ReturnInvoiceId { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
