using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;
namespace NEXT.Domain.Entities;
public sealed class CashTransfer : AuditableEntity<Guid>
{
    public Guid OperationId { get; set; }
    public AccountType FromAccount { get; set; }
    public AccountType ToAccount { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string Reason { get; set; } = "";
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = "";
}
