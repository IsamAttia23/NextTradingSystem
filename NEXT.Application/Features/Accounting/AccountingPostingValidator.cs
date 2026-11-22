using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Application.Features.Accounting;

// Application rules use locked persistence facts; Infrastructure only loads and writes them.
public sealed class AccountingPostingValidator(IAccountingStore store)
{
    public async Task ValidateAsync(LedgerEntry entry, CancellationToken ct)
    {
        await ReversalAsync(entry, ct);
        await PaymentAsync(entry, ct);
        await DebtsAsync(entry, ct);
    }

    private async Task ReversalAsync(LedgerEntry entry, CancellationToken ct)
    {
        if (entry.ReversesEntryId is not { } id) return;
        var original = await store.FindEntryAsync(id, ct) ?? throw new NotFoundException("AccountingEntryNotFound");
        if (original.PostedAt is null || original.Kind == LedgerEntryKind.Reversal || entry.EntryDate < original.EntryDate ||
            entry.InvoiceId != original.InvoiceId || entry.PaymentId != original.PaymentId || await store.HasReversalAsync(id, ct))
            throw new ConflictException("AccountingReversalUnavailable");
        var left = original.Lines.OrderBy(line => line.Sequence).ToArray();
        var right = entry.Lines.OrderBy(line => line.Sequence).ToArray();
        if (left.Length != right.Length || left.Where((line, index) => line.AccountId != right[index].AccountId ||
                line.CustomerId != right[index].CustomerId || line.DebtItemId != right[index].DebtItemId ||
                line.Debit != right[index].Credit || line.Credit != right[index].Debit).Any())
            throw new ConflictException("AccountingReversalUnavailable");
    }

    private async Task PaymentAsync(LedgerEntry entry, CancellationToken ct)
    {
        if (entry.Kind is not (LedgerEntryKind.Receipt or LedgerEntryKind.Refund)) return;
        var payment = await store.FindPaymentAsync(entry.PaymentId!.Value, ct);
        var cash = entry.Lines.Where(line => line.AccountId == Account.IdFor(AccountType.Treasury)).Sum(line => line.Debit - line.Credit);
        if (payment is null || !MoneyRules.IsValid(payment.Amount) || payment.Amount <= 0 ||
            payment.InvoiceId != entry.InvoiceId || payment.Date != entry.EntryDate ||
            entry.Lines.Any(line => line.CustomerId != payment.CustomerId) ||
            (entry.Kind == LedgerEntryKind.Receipt && (payment.Type != PaymentType.Receipt || cash != payment.Amount)) ||
            (entry.Kind == LedgerEntryKind.Refund && (payment.Type != PaymentType.Payment || cash != -payment.Amount)))
            throw new ConflictException("AccountingPaymentMismatch");
    }

    private async Task DebtsAsync(LedgerEntry entry, CancellationToken ct)
    {
        var ids = entry.Lines.Where(line => line.DebtItemId.HasValue).Select(line => line.DebtItemId!.Value).Distinct().Order().ToArray();
        if (ids.Length == 0)
        {
            if (entry.ReferenceType == AccountingReferenceType.DebtOpening) throw new ConflictException("AccountingInvalidReference");
            return;
        }
        var items = await store.LockDebtsAsync(ids, ct);
        if (items.Count != ids.Length) throw new ConflictException("AccountingDebtUnavailable");
        foreach (var item in items)
        {
            var lines = entry.Lines.Where(line => line.DebtItemId == item.Id).ToArray();
            if (lines.Any(line => line.CustomerId != item.CustomerId) || !item.CustomerActive || string.IsNullOrWhiteSpace(item.Phone))
                throw new ConflictException("AccountingDebtCustomerInvalid");
            var change = lines.Sum(line => line.Debit - line.Credit);
            if (change < 0 && !MoneyRules.CanSettle(item.Balance, -change)) throw new ConflictException("AccountingOverpayment");
            if (item.Balance + change > MoneyRules.Maximum) throw new ConflictException("AccountingBalanceLimit");
            if (entry.ReferenceType == AccountingReferenceType.DebtOpening &&
                (ids.Length != 1 || item.Id != entry.ReferenceId || item.Kind != DebtItemKind.OpeningBalance ||
                 item.Balance != 0 || change != item.OriginalAmount))
                throw new ConflictException("AccountingInvalidReference");
        }
    }
}
