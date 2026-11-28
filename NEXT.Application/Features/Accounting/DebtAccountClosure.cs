using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Accounting;

public sealed class DebtAccountClosure(IInvoiceAccountingStore store, IReadRepository<AccountTransaction> lines,
    IReadRepository<Invoice> invoices, IReadRepository<DebtItem> debts, IQueryExecutor executor, IUnitOfWork work, TimeProvider clock)
{
    public async Task TryCloseAsync(Guid accountId, Guid actorId, CancellationToken ct)
    {
        var account = await store.LockDebtAccountByIdAsync(accountId, ct)
            ?? throw new ConflictException("AccountingDebtUnavailable");
        if (account.ClosedAt.HasValue) return;
        await work.SaveChangesAsync(ct);
        var amounts = await executor.ToListAsync(lines.Query(t => t.DebtItem != null &&
            t.DebtItem.DebtAccountId == accountId && t.LedgerEntry.PostedAt != null)
            .Select(t => new { Amount = t.Debit - t.Credit, t.DebtItemId }), ct);
        if (amounts.Sum(t => t.Amount) != 0 || await invoices.AnyAsync(i => i.DebtAccountId == accountId && i.RemainingAmount > 0, ct)) return;
        var debtIds = await executor.ToListAsync(debts.Query(d => d.DebtAccountId == accountId).Select(d => new { d.Id }), ct);
        var postedIds = amounts.Select(t => t.DebtItemId).ToHashSet();
        if(debtIds.Any(d => !postedIds.Contains(d.Id))) return;
        account.Close(actorId, clock.GetUtcNow());
        await work.SaveChangesAsync(ct);
    }
}
