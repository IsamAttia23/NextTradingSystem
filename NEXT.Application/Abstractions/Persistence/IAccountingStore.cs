using NEXT.Domain.Entities;
using NEXT.Application.DTOs.Accounting;

namespace NEXT.Application.Abstractions.Persistence;

// Append-only financial writes; shared repository contracts continue to supply reads.
public interface IAccountingStore
{
    Task LockPostingAsync(Guid operationId, string postingKey, CancellationToken ct);
    Task<LedgerEntry?> FindOperationAsync(Guid operationId, CancellationToken ct);
    Task<LedgerEntry?> FindSourceAsync(string postingKey, CancellationToken ct);
    Task<LedgerEntry?> FindEntryAsync(Guid id, CancellationToken ct);
    Task<bool> HasReversalAsync(Guid entryId, CancellationToken ct);
    Task<PaymentPostingState?> FindPaymentAsync(Guid id, CancellationToken ct);
    Task<InvoicePostingState?> LockInvoiceAsync(Guid id, CancellationToken ct);
    Task<decimal> GetInvoiceCreditBalanceAsync(Guid id, NEXT.Domain.Enums.AccountType account, CancellationToken ct);
    Task<IReadOnlyList<DebtPostingState>> LockDebtsAsync(Guid[] ids, CancellationToken ct);
    Task AppendAsync(LedgerEntry entry, DateTimeOffset postedAt, CancellationToken ct);
}
