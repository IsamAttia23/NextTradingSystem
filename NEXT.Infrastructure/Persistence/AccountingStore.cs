using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence;

public sealed class AccountingStore(NEXTDbContext db) : IAccountingStore
{
    public async Task LockPostingAsync(Guid operationId, string postingKey, CancellationToken ct)
    {
        RequireTransaction();
        if (!db.Database.IsNpgsql()) return;
        foreach (var key in new[] { "accounting:operation:" + operationId.ToString("N"), "accounting:source:" + postingKey }.Order(StringComparer.Ordinal))
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }

    public Task<LedgerEntry?> FindOperationAsync(Guid operationId, CancellationToken ct) =>
        db.LedgerEntries.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
    public Task<LedgerEntry?> FindSourceAsync(string postingKey, CancellationToken ct) =>
        db.LedgerEntries.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.PostingKey == postingKey, ct);
    public Task<LedgerEntry?> FindEntryAsync(Guid id, CancellationToken ct) =>
        db.LedgerEntries.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, ct);
    public Task<bool> HasReversalAsync(Guid entryId, CancellationToken ct) =>
        db.LedgerEntries.AnyAsync(item => item.ReversesEntryId == entryId, ct);

    public async Task<PaymentPostingState?> FindPaymentAsync(Guid id, CancellationToken ct)
    {
        var payment = db.Payments.Local.FirstOrDefault(item => item.Id == id) ??
            await db.Payments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        return payment is null ? null : new(payment.InvoiceId, payment.CustomerId, payment.Amount, payment.PaymentType, payment.PaymentDate);
    }

    public async Task<IReadOnlyList<DebtPostingState>> LockDebtsAsync(Guid[] ids, CancellationToken ct)
    {
        RequireTransaction();
        var query = db.Database.IsNpgsql()
            ? db.DebtItems.FromSqlInterpolated($"SELECT * FROM \"DebtItems\" WHERE \"Id\" = ANY ({ids}) ORDER BY \"Id\" FOR UPDATE")
            : db.DebtItems.Where(item => ids.Contains(item.Id)).OrderBy(item => item.Id);
        var items = await query.AsTracking().Include(item => item.DebtAccount).ThenInclude(account => account.Customer).ToListAsync(ct);
        var totals = await db.AccountTransactions.Where(line => line.DebtItemId.HasValue && ids.Contains(line.DebtItemId.Value) &&
                line.LedgerEntry.PostedAt != null).GroupBy(line => line.DebtItemId!.Value)
            .Select(group => new { Id = group.Key, Balance = group.Sum(line => line.Debit - line.Credit) })
            .ToDictionaryAsync(item => item.Id, item => item.Balance, ct);
        return items.Select(item => new DebtPostingState(item.Id, item.DebtAccount.CustomerId,
            item.DebtAccount.Customer.IsActive && !item.DebtAccount.Customer.DeletedAt.HasValue,
            item.DebtAccount.Customer.Phone, item.Kind, item.OriginalAmount, totals.GetValueOrDefault(item.Id))).ToArray();
    }

    public async Task AppendAsync(LedgerEntry entry, DateTimeOffset postedAt, CancellationToken ct)
    {
        RequireTransaction();
        entry.Validate();
        if (entry.PostedAt is not null) throw new ConflictException("AccountingEntryImmutable");
        var catalog = await db.Accounts.AsNoTracking().ToListAsync(ct);
        if (catalog.Count != Account.SeedData().Count || entry.Lines.Any(line => catalog.All(account => account.Id != line.AccountId)))
            throw new ConflictException("AccountingSeedRequired");
        db.LedgerEntries.Add(entry);
        // Both saves share the caller's transaction. The database rejects an unfinalized commit.
        await db.SaveChangesAsync(ct);
        entry.MarkPosted(postedAt);
        await db.SaveChangesAsync(ct);
    }

    private void RequireTransaction()
    {
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Accounting writes require a transaction.");
    }
}