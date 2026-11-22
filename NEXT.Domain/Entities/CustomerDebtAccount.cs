using NEXT.Domain.Common.Entities;

namespace NEXT.Domain.Entities;

public sealed class CustomerDebtAccount : AuditableEntity<Guid>
{
    private CustomerDebtAccount() { }
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public DateTimeOffset LastActivityAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static CustomerDebtAccount Create(Guid customerId, Guid actorId, DateTimeOffset now)
    {
        if (customerId == Guid.Empty || actorId == Guid.Empty) throw new ArgumentException("AccountingInvalidIdentifier");
        return new() { Id = Guid.NewGuid(), CustomerId = customerId, CreatedBy = actorId, LastActivityAt = now.ToUniversalTime() };
    }

    public void RecordActivity(DateTimeOffset now) => LastActivityAt = now.ToUniversalTime();
}
