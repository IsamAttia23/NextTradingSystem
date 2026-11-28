using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;
namespace NEXT.Domain.Entities;

public sealed class DebtAccountPayment : AuditableEntity<Guid>
{
    public Guid DebtAccountId { get; set; }
    public string Number { get; set; } = "";
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public AccountType CashAccount { get; set; }
    public DateOnly Date { get; set; }
    public string? Notes { get; set; }
    public string RequestHash { get; set; } = "";
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = "";
}
