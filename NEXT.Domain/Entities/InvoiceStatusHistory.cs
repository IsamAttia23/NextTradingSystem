using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public sealed class InvoiceStatusHistory : AuditableEntity<Guid>
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public Guid OperationId { get; set; }
    public InvoiceStatus FromStatus { get; set; }
    public InvoiceStatus ToStatus { get; set; }
    public DateTimeOffset ActionAt { get; set; }
    public Guid ActorId { get; set; }
    public string ActorFullName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public decimal PaymentAmount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
}
