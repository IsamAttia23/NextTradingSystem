using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public sealed class AccountTransaction
{
    private AccountTransaction() { }
    public Guid Id { get; private set; }
    public Guid LedgerEntryId { get; private set; }
    public LedgerEntry LedgerEntry { get; private set; } = null!;
    public short Sequence { get; private set; }
    public Guid AccountId { get; private set; }
    public Account Account { get; private set; } = null!;
    public Guid? CustomerId { get; private set; }
    public Guid? DebtItemId { get; private set; }
    public DebtItem? DebtItem { get; private set; }
    public string? Description { get; private set; }
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }

    public static AccountTransaction Create(Guid entryId, short sequence, AccountType account, decimal debit,
        decimal credit, Guid? customerId = null, Guid? debtItemId = null, string? description = null)
    {
        if (entryId == Guid.Empty || sequence < 1 || customerId == Guid.Empty || debtItemId == Guid.Empty)
            throw new ArgumentException("AccountingInvalidIdentifier");
        if (!Services.MoneyRules.IsValid(debit) || !Services.MoneyRules.IsValid(credit) || (debit > 0) == (credit > 0))
            throw new ArgumentException("AccountingInvalidAmount");
        if ((account == AccountType.Receivables && (customerId is null || debtItemId is null)) ||
            (account != AccountType.Receivables && debtItemId is not null))
            throw new ArgumentException("AccountingDebtReferenceRequired");
        if (description?.Length > 500) throw new ArgumentException("AccountingDescriptionInvalid");
        return new() { Id = Guid.NewGuid(), LedgerEntryId = entryId, Sequence = sequence,
            AccountId = global::NEXT.Domain.Entities.Account.IdFor(account), Debit = debit, Credit = credit,
            CustomerId = customerId, DebtItemId = debtItemId, Description = description };
    }
}
