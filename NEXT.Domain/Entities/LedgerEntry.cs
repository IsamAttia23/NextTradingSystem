using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Domain.Entities;

public sealed class LedgerEntry : AuditableEntity<Guid>
{
    private LedgerEntry() { }
    private readonly List<AccountTransaction> _lines = [];
    public Guid OperationId { get; private set; }
    public string PostingKey { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public LedgerEntryKind Kind { get; private set; }
    public AccountingReferenceType ReferenceType { get; private set; }
    public Guid ReferenceId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public Invoice? Invoice { get; private set; }
    public Guid? PaymentId { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public string Description { get; private set; } = "";
    public Guid ActorId { get; private set; }
    public string ActorName { get; private set; } = "";
    public DateTimeOffset? PostedAt { get; private set; }
    public Guid? ReversesEntryId { get; private set; }
    public IReadOnlyCollection<AccountTransaction> Lines => _lines.AsReadOnly();

    public static LedgerEntry Create(Guid id, Guid operationId, LedgerEntryKind kind, AccountingReferenceType referenceType,
        Guid referenceId, Guid? invoiceId, Guid? paymentId, DateOnly date, string description, Guid actorId,
        string actorName, string requestHash, IEnumerable<AccountTransaction> lines, Guid? reversesEntryId = null)
    {
        if (id == Guid.Empty || operationId == Guid.Empty || referenceId == Guid.Empty || actorId == Guid.Empty ||
            invoiceId == Guid.Empty || paymentId == Guid.Empty || reversesEntryId == Guid.Empty)
            throw new ArgumentException("AccountingInvalidIdentifier");
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(referenceType)) throw new ArgumentException("AccountingInvalidReference");
        if (string.IsNullOrWhiteSpace(description) || description.Length > 500 ||
            string.IsNullOrWhiteSpace(actorName) || actorName.Length > 200 || requestHash.Length != 64)
            throw new ArgumentException("AccountingDescriptionInvalid");
        var entry = new LedgerEntry { Id = id, OperationId = operationId, Kind = kind, ReferenceType = referenceType,
            ReferenceId = referenceId, InvoiceId = invoiceId, PaymentId = paymentId, EntryDate = date,
            Description = description, ActorId = actorId, ActorName = actorName, RequestHash = requestHash,
            PostingKey = Key(kind, referenceType, referenceId), ReversesEntryId = reversesEntryId, CreatedBy = actorId };
        entry._lines.AddRange(lines);
        entry.Validate();
        return entry;
    }

    public static string Key(LedgerEntryKind kind, AccountingReferenceType referenceType, Guid referenceId) =>
        $"{(int)kind}:{(int)referenceType}:{referenceId:N}";

    public void Validate()
    {
        var validSource = Kind switch
        {
            LedgerEntryKind.SalesRecognition or LedgerEntryKind.SalesReturn or LedgerEntryKind.InvoiceCancellation => ReferenceType == AccountingReferenceType.Invoice,
            LedgerEntryKind.Receipt or LedgerEntryKind.Refund => ReferenceType == AccountingReferenceType.Payment,
            LedgerEntryKind.OpeningBalance => ReferenceType is AccountingReferenceType.DebtOpening or AccountingReferenceType.Adjustment or AccountingReferenceType.OpeningAdoption,
            LedgerEntryKind.Reversal => ReferenceType == AccountingReferenceType.Adjustment,
            LedgerEntryKind.GoodsReceipt => ReferenceType == AccountingReferenceType.InventoryReceipt && InvoiceId is null && PaymentId is null,
            LedgerEntryKind.CashTransfer => ReferenceType == AccountingReferenceType.CashTransfer && InvoiceId is null && PaymentId is null,
            _ => false
        };
        if (!validSource) throw new ArgumentException("AccountingInvalidReference");
        if (_lines.Count is < 2 or > 1000 || _lines.Any(line => line.LedgerEntryId != Id) ||
            _lines.Select(line => line.Sequence).Distinct().Count() != _lines.Count)
            throw new ArgumentException("AccountingLinesInvalid");
        var debit = _lines.Sum(line => line.Debit);
        if (debit != _lines.Sum(line => line.Credit) || debit <= 0 || debit > MoneyRules.Maximum)
            throw new ArgumentException("AccountingUnbalancedEntry");
        if ((ReferenceType == AccountingReferenceType.Invoice && InvoiceId != ReferenceId) ||
            (ReferenceType == AccountingReferenceType.Payment && PaymentId != ReferenceId) ||
            (Kind == LedgerEntryKind.Reversal) != ReversesEntryId.HasValue ||
            (ReversesEntryId.HasValue && (ReferenceType != AccountingReferenceType.Adjustment || ReferenceId != ReversesEntryId)))
            throw new ArgumentException("AccountingInvalidReference");
    }

    public void MarkPosted(DateTimeOffset at)
    {
        if (PostedAt is not null) throw new InvalidOperationException("AccountingEntryImmutable");
        Validate();
        PostedAt = at.ToUniversalTime();
    }
}
