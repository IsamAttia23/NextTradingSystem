using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;
namespace NEXT.Domain.Entities;
public sealed class CashReconciliation : AuditableEntity<Guid>
{
    public Guid OperationId { get; set; }
    public AccountType Account { get; set; }
    public DateOnly Date { get; set; }
    public decimal BookBalance { get; set; }
    public decimal ExternalBalance { get; set; }
    public decimal DepositsInTransit { get; set; }
    public decimal OutstandingPayments { get; set; }
    public string PositionHash { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string Reason { get; set; } = "";
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = "";
    public decimal Difference => BookBalance - (ExternalBalance + DepositsInTransit - OutstandingPayments);
}
