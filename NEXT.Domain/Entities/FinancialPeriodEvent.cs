using NEXT.Domain.Common.Entities;
namespace NEXT.Domain.Entities;
public sealed class FinancialPeriodEvent : AuditableEntity<Guid>
{
    public Guid PeriodId { get; set; }
    public long Version { get; set; }
    public bool IsClosed { get; set; }
    public string Reason { get; set; } = "";
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = "";
}
