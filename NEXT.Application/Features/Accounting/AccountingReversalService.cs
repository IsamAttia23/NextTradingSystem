using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Application.Features.Accounting;

// Low-level ledger reversal; invoice/refund workflows must coordinate their own source records.
public sealed class AccountingReversalService(IAccountingStore store, IAccountingPostingService posting)
    : IAccountingReversalService
{
    public async Task<Guid> ReverseAsync(ReverseEntryInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequestValidation.Identifier(input.EntryId, nameof(input.EntryId));
        var reason = RequestValidation.Text(input.Reason, nameof(input.Reason), 500);
        var original = await store.FindEntryAsync(input.EntryId, ct) ?? throw new NotFoundException("AccountingEntryNotFound");
        if (original.ReferenceType == AccountingReferenceType.OpeningAdoption || original.Kind is LedgerEntryKind.GoodsReceipt or LedgerEntryKind.CashTransfer || original.InvoiceId.HasValue || original.PaymentId.HasValue || original.Lines.Any(line => line.DebtItemId.HasValue))
            throw new ConflictException("AccountingUseSourceCorrection");
        if (original.PostedAt is null || original.Kind == LedgerEntryKind.Reversal)
            throw new ConflictException("AccountingReversalUnavailable");
        var types = Account.SeedData().ToDictionary(item => item.Id, item => item.Type);
        var lines = original.Lines.OrderBy(line => line.Sequence).Select(line => new PostingLineDto(types[line.AccountId],
            line.Credit, line.Debit, line.CustomerId, line.DebtItemId, line.Description)).ToArray();
        return await posting.PostAsync(new(input.OperationId, LedgerEntryKind.Reversal, AccountingReferenceType.Adjustment,
            original.Id, input.Date, reason, lines, original.InvoiceId, original.PaymentId, original.Id), ct);
    }
}
