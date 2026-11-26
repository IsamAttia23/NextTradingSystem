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
        RequireTransaction();
        var pending = db.ChangeTracker.Entries<Payment>().SingleOrDefault(item => item.Entity.Id == id && item.State == EntityState.Added)?.Entity;
        var query = db.Database.IsNpgsql()
            ? db.Payments.FromSqlInterpolated($"SELECT * FROM \"Payments\" WHERE \"Id\" = {id} FOR UPDATE")
            : db.Payments.Where(item => item.Id == id);
        var payment = pending ?? await query.AsNoTracking().SingleOrDefaultAsync(ct);
        return payment is null ? null : new(payment.InvoiceId, payment.CustomerId, payment.Amount, payment.PaymentType, payment.PaymentDate, payment.Purpose, payment.CashAccount);
    }

    public async Task<InvoicePostingState?> LockInvoiceAsync(Guid id, CancellationToken ct)
    {
        RequireTransaction();
        var query = db.Database.IsNpgsql()
            ? db.Invoices.FromSqlInterpolated($"SELECT * FROM \"Invoices\" WHERE \"Id\" = {id} FOR UPDATE")
            : db.Invoices.Where(item => item.Id == id);
        var invoice = await query.AsTracking().SingleOrDefaultAsync(ct);
        return invoice is null ? null : new(invoice.Id, invoice.Type, invoice.Status, invoice.TotalAmount,
            invoice.PaidAmount, invoice.RemainingAmount, invoice.CustomerId, invoice.TaxAmount, invoice.LegacyInvoiceTypeCode, invoice.ParentInvoiceId, invoice.InvoiceDate);
    }

    public async Task<decimal> GetInvoiceCreditBalanceAsync(Guid id, NEXT.Domain.Enums.AccountType account, CancellationToken ct)
    {
        var accountId = Account.IdFor(account);
        return await db.AccountTransactions.Where(line => line.AccountId == accountId && line.LedgerEntry.InvoiceId == id &&
            line.LedgerEntry.PostedAt != null).SumAsync(line => (decimal?)(line.Credit - line.Debit), ct) ?? 0;
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
            item.DebtAccount.Customer.Phone, item.Kind, item.OriginalAmount, totals.GetValueOrDefault(item.Id), item.InvoiceId)).ToArray();
    }

    public async Task<InventoryPostingState> GetInventorySourceAsync(Guid id, bool invoice, CancellationToken ct)
    {
        var reference = invoice ? "Invoice" : "Receipt";
        var saved = await db.StockMovements.AsNoTracking().Where(m => m.ReferenceId == id && m.ReferenceType == reference).ToListAsync(ct);
        var pending = db.ChangeTracker.Entries<StockMovement>().Where(e => e.State == EntityState.Added &&
            e.Entity.ReferenceId == id && e.Entity.ReferenceType == reference).Select(e => e.Entity);
        var movements = saved.Concat(pending).DistinctBy(m => m.Id).ToArray();
        var valid = movements.Length > 0 && movements.All(m => NEXT.Domain.Services.MoneyRules.IsValid(m.TotalCost) && m.Quantity > 0 &&
            (invoice ? m.Type == NEXT.Domain.Enums.StockMovementType.Sale :
                m.Type is NEXT.Domain.Enums.StockMovementType.Purchase or NEXT.Domain.Enums.StockMovementType.OpeningBalance));
        if (invoice)
        {
            var items = await db.InvoiceItems.AsNoTracking().Where(i => i.InvoiceId == id && i.ProductId.HasValue)
                .Select(i => new { ProductId = i.ProductId!.Value, i.Quantity }).ToListAsync(ct);
            var expected = items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
            var actual = movements.GroupBy(m => m.ProductId).ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));
            var warehouse = await db.Invoices.Where(i => i.Id == id).Select(i => i.WarehouseId).SingleAsync(ct);
            valid = await db.InvoiceItems.AnyAsync(i => i.InvoiceId == id, ct) && (movements.Length == 0 || valid) && movements.All(m => m.WarehouseId == warehouse) && actual.Count == expected.Count && expected.All(p => actual.GetValueOrDefault(p.Key) == p.Value);
        }
        else valid &= movements.Length == 1;
        return new(movements.Sum(m => m.TotalCost), movements.Length, valid,
            movements.Length == 1 && movements[0].Type == NEXT.Domain.Enums.StockMovementType.OpeningBalance,
            movements.Length == 1 ? movements[0].SupplierId : null);
    }

    public async Task<OpeningPosition> ReadOpeningPositionAsync(NEXT.Domain.Enums.AccountType account, bool lockStocks, CancellationToken ct)
    {
        if (lockStocks) RequireTransaction();
        var accountId = Account.IdFor(account);
        var lines = await db.AccountTransactions.AsNoTracking().Where(t => t.AccountId == accountId && t.LedgerEntry.PostedAt != null)
            .OrderBy(t => t.Id).Select(t => new { t.Id, t.Debit, t.Credit, t.LedgerEntry.EntryDate }).ToListAsync(ct);
        var book = lines.Sum(t => t.Debit - t.Credit);
        decimal? inventory = null;
        DateTimeOffset? stockAt = null;
        var stockHash = "";
        DateOnly? latest = lines.Count == 0 ? null : lines.Max(t => t.EntryDate);
        if (account == NEXT.Domain.Enums.AccountType.Inventory)
        {
            var query = lockStocks && db.Database.IsNpgsql()
                ? db.Stocks.FromSqlRaw("SELECT * FROM \"Stocks\" ORDER BY \"WarehouseId\", \"ProductId\" FOR UPDATE")
                : db.Stocks.OrderBy(s => s.WarehouseId).ThenBy(s => s.ProductId).AsQueryable();
            var stocks = await query.AsNoTracking().ToListAsync(ct);
            inventory = stocks.Sum(s => s.TotalCost);
            stockHash = System.Text.Json.JsonSerializer.Serialize(stocks.Select(s => new { s.Id, s.Quantity, s.ReservedQuantity, s.TotalCost, s.RowVersion }));
            if (stocks.Count > 0)
            {
                stockAt = stocks.Max(s => s.LastUpdated);
            }
        }
        var payload = System.Text.Json.JsonSerializer.Serialize(new { account, lines, stockHash });
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
        return new(account, book, inventory, hash, latest, await db.AccountOpenings.AnyAsync(o => o.Account == account, ct), stockAt);
    }

    public Task<AccountOpening?> FindOpeningAsync(Guid id, CancellationToken ct) =>
        db.AccountOpenings.Local.FirstOrDefault(o => o.Id == id) is { } local ? Task.FromResult<AccountOpening?>(local) :
            db.AccountOpenings.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, ct);

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
