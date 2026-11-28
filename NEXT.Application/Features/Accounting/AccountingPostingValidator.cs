using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.Services;

namespace NEXT.Application.Features.Accounting;

// Application rules use locked persistence facts; Infrastructure only loads and writes them.
public sealed class AccountingPostingValidator(IAccountingStore store, TimeProvider clock, IFinancialControlStore controls)
{
    public async Task ValidateAsync(LedgerEntry entry, CancellationToken ct)
    {
        var inventoryAccounts = new[] { Account.IdFor(AccountType.Inventory), Account.IdFor(AccountType.CostOfSales), Account.IdFor(AccountType.GoodsReceivedNotInvoiced) };
        if (entry.ReferenceType != AccountingReferenceType.OpeningAdoption && entry.Kind is not (LedgerEntryKind.SalesRecognition or LedgerEntryKind.GoodsReceipt) && entry.Lines.Any(l => inventoryAccounts.Contains(l.AccountId)))
            throw new ConflictException("AccountingUseSourceCorrection");
        if (entry.Kind == LedgerEntryKind.CashTransfer)
        {
            var transfer = await controls.TransferAsync(entry.OperationId, ct);
            if (transfer is null || transfer.Id != entry.ReferenceId || transfer.Date != entry.EntryDate || transfer.ActorId != entry.ActorId ||
                entry.Lines.Count != 2 || entry.Lines.Any(l => l.CustomerId.HasValue || l.DebtItemId.HasValue) ||
                !entry.Lines.Any(l => l.AccountId == Account.IdFor(transfer.ToAccount) && l.Debit == transfer.Amount) ||
                !entry.Lines.Any(l => l.AccountId == Account.IdFor(transfer.FromAccount) && l.Credit == transfer.Amount))
                throw new ConflictException("CashTransferInvalid");
        }
        await OpeningAsync(entry, ct);
        await InventoryReceiptAsync(entry, ct);
        await ReversalAsync(entry, ct);
        await InvoiceAsync(entry, ct);
        await PaymentAsync(entry, ct);
        await DebtsAsync(entry, ct);
    }

    private async Task OpeningAsync(LedgerEntry entry, CancellationToken ct)
    {
        if (entry.ReferenceType != AccountingReferenceType.OpeningAdoption) return;
        var document = await store.FindOpeningAsync(entry.ReferenceId, ct);
        if (document is null || entry.Kind != LedgerEntryKind.OpeningBalance || entry.InvoiceId.HasValue || entry.PaymentId.HasValue ||
            document.OperationId != entry.OperationId || document.Date != entry.EntryDate || document.ActorId != entry.ActorId || entry.Lines.Count != 2 ||
            entry.Lines.Any(l => l.CustomerId.HasValue || l.DebtItemId.HasValue)) throw new ConflictException("AccountingOpeningInvalid");
        var position = await store.ReadOpeningPositionAsync(document.Account, true, ct);
        decimal Net(AccountType type) => entry.Lines.Where(l => l.AccountId == Account.IdFor(type)).Sum(l => l.Debit - l.Credit);
        if (position.LatestDate > entry.EntryDate ||
            (position.LatestStockAt is { } at && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone).DateTime) > entry.EntryDate) ||
            position.Hash != document.PositionHash || position.BookBalance != document.BeforeBalance ||
            (document.Account == AccountType.Inventory && document.VerifiedBalance != position.InventoryValue) ||
            Net(document.Account) != document.Difference || Net(AccountType.OpeningBalances) != -document.Difference)
            throw new ConflictException("AccountingOpeningStale");
    }

    private async Task InventoryReceiptAsync(LedgerEntry entry, CancellationToken ct)
    {
        if (entry.Kind != LedgerEntryKind.GoodsReceipt) return;
        var source = await store.GetInventorySourceAsync(entry.ReferenceId, false, ct);
        var counter = Account.IdFor(source.IsOpening ? AccountType.OpeningBalances : AccountType.GoodsReceivedNotInvoiced);
        var total = source.TotalCost + source.CostAdjustment;
        decimal Net(Guid account) => entry.Lines.Where(l => l.AccountId == account).Sum(l => l.Debit - l.Credit);
        if (!source.IsValidSource || source.TotalCost < 0 || !MoneyRules.IsValid(total) ||
            (!source.IsOpening && source.SupplierId is null) ||
            entry.Lines.Any(l => l.CustomerId.HasValue || l.DebtItemId.HasValue) ||
            Net(Account.IdFor(AccountType.Inventory)) != source.TotalCost || Net(counter) != -total ||
            Net(Account.IdFor(AccountType.CostOfSales)) != source.CostAdjustment ||
            entry.Lines.Count != (source.TotalCost > 0 ? 1 : 0) + (total > 0 ? 1 : 0) + (source.CostAdjustment != 0 ? 1 : 0))
            throw new ConflictException("AccountingInventoryPostingMismatch");
    }

    private async Task ReversalAsync(LedgerEntry entry, CancellationToken ct)
    {
        if (entry.ReversesEntryId is not { } id) return;
        var original = await store.FindEntryAsync(id, ct) ?? throw new NotFoundException("AccountingEntryNotFound");
        if (original.ReferenceType == AccountingReferenceType.OpeningAdoption || original.Kind is LedgerEntryKind.GoodsReceipt or LedgerEntryKind.CashTransfer || original.InvoiceId.HasValue || original.PaymentId.HasValue || original.Lines.Any(line => line.DebtItemId.HasValue))
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
        var cash = entry.Lines.Where(line => line.AccountId == Account.IdFor(payment == null ? AccountType.Treasury : payment.CashAccount)).Sum(line => line.Debit - line.Credit);
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
        var inventory = entry.Kind == LedgerEntryKind.SalesRecognition ? await store.GetInventorySourceAsync(id, true, ct) : null;
        var cost = inventory?.TotalCost ?? 0;
        var valid = entry.Kind switch
        {
            LedgerEntryKind.SalesRecognition => invoice.Status is (InvoiceStatus.Shipped or InvoiceStatus.Delivered) &&
                inventory!.IsValidSource && invoice.Tax == 0 && invoice.Total >= 0 && invoice.Paid >= 0 && invoice.Remaining >= 0 &&
                await store.GetInvoiceCreditBalanceAsync(id, AccountType.CustomerAdvances, ct) == invoice.Paid &&
                invoice.Paid + invoice.Remaining == invoice.Total && Net(AccountType.Sales) == -invoice.Total &&
                Net(AccountType.CustomerAdvances) == invoice.Paid && Net(AccountType.Receivables) == invoice.Remaining &&
                Net(AccountType.CostOfSales) == cost && Net(AccountType.Inventory) == -cost &&
                entry.Lines.Count == (invoice.Total > 0 ? 1 : 0) + (invoice.Paid > 0 ? 1 : 0) + (invoice.Remaining > 0 ? 1 : 0) + (cost > 0 ? 2 : 0),
            LedgerEntryKind.InvoiceCancellation => invoice.Status == InvoiceStatus.Cancelled && invoice.Remaining == 0 && invoice.Paid > 0 &&
                await store.GetInvoiceCreditBalanceAsync(id, AccountType.CustomerAdvances, ct) == invoice.Paid &&
                Net(AccountType.CustomerAdvances) == invoice.Paid && Net(AccountType.CustomerRefunds) == -invoice.Paid && entry.Lines.Count == 2,
            LedgerEntryKind.Receipt => invoice.Status != InvoiceStatus.Cancelled && await ValidReceiptAsync(entry, invoice, ct),
            LedgerEntryKind.Refund => invoice.Status == InvoiceStatus.Cancelled && entry.Lines.Count == 2 &&
                Net(AccountType.CustomerRefunds) == -entry.Lines.Where(l => NEXT.Domain.Services.CashAccounts.All.Select(Account.IdFor).Contains(l.AccountId)).Sum(l => l.Debit - l.Credit) && Net(AccountType.CustomerRefunds) > 0 &&
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
        var cash = entry.Lines.SingleOrDefault(l => NEXT.Domain.Services.CashAccounts.All.Select(Account.IdFor).Contains(l.AccountId))?.AccountId ?? Guid.Empty;
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
