using NEXT.Domain.Common.Entities;

namespace NEXT.Domain.Entities;

public sealed class CustomerDebtAccount : AuditableEntity<Guid>
{
    private CustomerDebtAccount() { }
    public int CycleNumber { get; private set; } = 1;
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public DateTimeOffset LastActivityAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static CustomerDebtAccount Create(Guid customerId, Guid actorId, DateTimeOffset now, int cycleNumber = 1)
    {
        if (customerId == Guid.Empty || actorId == Guid.Empty || cycleNumber < 1) throw new ArgumentException("AccountingInvalidIdentifier");
        return new() { Id = Guid.NewGuid(), CustomerId = customerId, CycleNumber = cycleNumber, CreatedBy = actorId, LastActivityAt = now.ToUniversalTime() };
    }

    public void RecordActivity(DateTimeOffset now)
    {
        if (ClosedAt.HasValue) throw new InvalidOperationException("DebtBookAccountClosed");
        LastActivityAt = now.ToUniversalTime();
    }

    public void Close(Guid actorId, DateTimeOffset now)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("AccountingInvalidIdentifier");
        if (ClosedAt.HasValue) return;
        ClosedAt = now.ToUniversalTime();
        LastActivityAt = now.ToUniversalTime();
        ClosedBy = actorId;
    }
}
