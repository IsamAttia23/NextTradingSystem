using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Application.Features.Accounting;

// Application rules use locked persistence facts; Infrastructure only loads and writes them.
public sealed class AccountingPostingValidator(IAccountingStore store, TimeProvider clock)
{
    public async Task ValidateAsync(LedgerEntry entry, CancellationToken ct)
    {
        await ReversalAsync(entry, ct);
        await InvoiceAsync(entry, ct);
        await PaymentAsync(entry, ct);
        await DebtsAsync(entry, ct);
    }

    private async Task ReversalAsync(LedgerEntry entry, CancellationToken ct)
    {
        if (entry.ReversesEntryId is not { } id) return;
        var original = await store.FindEntryAsync(id, ct) ?? throw new NotFoundException("AccountingEntryNotFound");
        if (original.InvoiceId.HasValue || original.PaymentId.HasValue || original.Lines.Any(line => line.DebtItemId.HasValue))
            throw new ConflictException("AccountingUseSourceCorrection");
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
            (payment.InvoiceId.HasValue && payment.Purpose != (entry.Kind == LedgerEntryKind.Receipt ? PaymentPurpose.InvoiceReceipt : PaymentPurpose.CustomerRefund)) ||
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
            if (entry.Kind == LedgerEntryKind.SalesRecognition &&
                (item.InvoiceId != entry.InvoiceId || item.Kind != DebtItemKind.InvoiceBalance || change != item.OriginalAmount || item.Balance != 0))
                throw new ConflictException("AccountingInvoicePostingMismatch");
            if (change < 0 && !MoneyRules.CanSettle(item.Balance, -change)) throw new ConflictException("AccountingOverpayment");
            if (item.Balance + change > MoneyRules.Maximum) throw new ConflictException("AccountingBalanceLimit");
            if (entry.ReferenceType == AccountingReferenceType.DebtOpening &&
                (ids.Length != 1 || item.Id != entry.ReferenceId || item.Kind != DebtItemKind.OpeningBalance ||
                 item.Balance != 0 || change != item.OriginalAmount))
                throw new ConflictException("AccountingInvalidReference");
        }
    }

    private async Task InvoiceAsync(LedgerEntry entry, CancellationToken ct)
    {
        if (entry.InvoiceId is not { } id) return;
        var invoice = await store.LockInvoiceAsync(id, ct) ?? throw new NotFoundException("InvoiceNotFound");
        if (entry.EntryDate < DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(invoice.Date, clock.LocalTimeZone).DateTime))
            throw new ConflictException("AccountingPostingDateInvalid");
        if (invoice.Type != InvoiceType.Sales || invoice.LegacyType is not null || invoice.ParentInvoiceId is not null ||
            invoice.CustomerId is null || entry.Lines.Any(line => line.CustomerId != invoice.CustomerId))
            throw new ConflictException("AccountingInvoicePostingMismatch");
        decimal Net(AccountType type) => entry.Lines.Where(line => line.AccountId == Account.IdFor(type)).Sum(line => line.Debit - line.Credit);
        var valid = entry.Kind switch
        {
            LedgerEntryKind.SalesRecognition => invoice.Status is (InvoiceStatus.Shipped or InvoiceStatus.Delivered) &&
                invoice.Tax == 0 && invoice.Total > 0 && invoice.Paid >= 0 && invoice.Remaining >= 0 &&
                await store.GetInvoiceCreditBalanceAsync(id, AccountType.CustomerAdvances, ct) == invoice.Paid &&
                invoice.Paid + invoice.Remaining == invoice.Total && Net(AccountType.Sales) == -invoice.Total &&
                Net(AccountType.CustomerAdvances) == invoice.Paid && Net(AccountType.Receivables) == invoice.Remaining &&
                entry.Lines.Count == 1 + (invoice.Paid > 0 ? 1 : 0) + (invoice.Remaining > 0 ? 1 : 0),
            LedgerEntryKind.InvoiceCancellation => invoice.Status == InvoiceStatus.Cancelled && invoice.Remaining == 0 && invoice.Paid > 0 &&
                await store.GetInvoiceCreditBalanceAsync(id, AccountType.CustomerAdvances, ct) == invoice.Paid &&
                Net(AccountType.CustomerAdvances) == invoice.Paid && Net(AccountType.CustomerRefunds) == -invoice.Paid && entry.Lines.Count == 2,
            LedgerEntryKind.Receipt => invoice.Status != InvoiceStatus.Cancelled && await ValidReceiptAsync(entry, invoice, ct),
            LedgerEntryKind.Refund => invoice.Status == InvoiceStatus.Cancelled && entry.Lines.Count == 2 &&
                Net(AccountType.CustomerRefunds) == -Net(AccountType.Treasury) && Net(AccountType.CustomerRefunds) > 0 &&
                await store.GetInvoiceCreditBalanceAsync(id, AccountType.CustomerRefunds, ct) >= Net(AccountType.CustomerRefunds),
            _ => false
        };
        if (!valid) throw new ConflictException("AccountingInvoicePostingMismatch");
    }

    private async Task<bool> ValidReceiptAsync(LedgerEntry entry, NEXT.Application.DTOs.Accounting.InvoicePostingState invoice, CancellationToken ct)
    {
        var recognition = await store.FindSourceAsync(LedgerEntry.Key(LedgerEntryKind.SalesRecognition, AccountingReferenceType.Invoice, invoice.Id), ct);
        if (recognition is null && invoice.Status is (InvoiceStatus.Shipped or InvoiceStatus.Delivered)) return false;
        if (recognition is not null && (recognition.PostedAt is null || await store.HasReversalAsync(recognition.Id, ct))) return false;
        var counterpart = Account.IdFor(recognition is null ? AccountType.CustomerAdvances : AccountType.Receivables);
        var cash = Account.IdFor(AccountType.Treasury);
        var amount = entry.Lines.Where(line => line.AccountId == cash).Sum(line => line.Debit - line.Credit);
        if (recognition is null && await store.GetInvoiceCreditBalanceAsync(invoice.Id, AccountType.CustomerAdvances, ct) + amount != invoice.Paid)
            return false;
        if (recognition is not null)
        {
            var debtId = entry.Lines.SingleOrDefault(line => line.AccountId == counterpart)?.DebtItemId;
            if (debtId is null) return false;
            var debts = await store.LockDebtsAsync([debtId.Value], ct);
            if (debts.Count != 1 || debts[0].InvoiceId != invoice.Id || debts[0].Balance - amount != invoice.Remaining) return false;
        }
        return entry.Lines.Count == 2 && entry.Lines.Any(line => line.AccountId == cash && line.Debit > 0) &&
            entry.Lines.Any(line => line.AccountId == counterpart && line.Credit > 0);
    }
}
