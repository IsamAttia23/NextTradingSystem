using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Domain.Entities;

public sealed class DebtItem : AuditableEntity<Guid>
{
    private DebtItem() { }
    public Guid DebtAccountId { get; private set; }
    public CustomerDebtAccount DebtAccount { get; private set; } = null!;
    public DebtItemKind Kind { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public Invoice? Invoice { get; private set; }
    public decimal OriginalAmount { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public string Description { get; private set; } = "";

    public static DebtItem Create(Guid debtAccountId, DebtItemKind kind, Guid? invoiceId, decimal amount,
        DateOnly date, string description, Guid actorId)
    {
        if (debtAccountId == Guid.Empty || actorId == Guid.Empty || invoiceId == Guid.Empty)
            throw new ArgumentException("AccountingInvalidIdentifier");
        if (!Enum.IsDefined(kind) || (kind == DebtItemKind.InvoiceBalance) != invoiceId.HasValue)
            throw new ArgumentException("AccountingInvalidReference");
        if (!MoneyRules.IsValid(amount) || amount <= 0) throw new ArgumentException("AccountingInvalidAmount");
        if (string.IsNullOrWhiteSpace(description) || description.Length > 500)
            throw new ArgumentException("AccountingDescriptionInvalid");
        return new() { Id = Guid.NewGuid(), DebtAccountId = debtAccountId, Kind = kind, InvoiceId = invoiceId,
            OriginalAmount = amount, EffectiveDate = date, Description = description, CreatedBy = actorId };
    }
}
