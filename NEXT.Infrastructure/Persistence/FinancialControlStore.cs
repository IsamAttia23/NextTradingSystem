using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.DTOs.Accounting;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
namespace NEXT.Infrastructure.Persistence;
public sealed class FinancialControlStore(NEXTDbContext db) : IFinancialControlStore
{
    public async Task LockAsync(CancellationToken ct)
    {
        if (db.Database.IsNpgsql()) {
            if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("A transaction is required.");
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(21470125)", ct);
        }
    }
    public async Task EnsureOpenAsync(DateOnly date, CancellationToken ct)
    {
        await LockAsync(ct);
        if (await db.FinancialPeriods.AnyAsync(ct) && !await db.FinancialPeriods.AnyAsync(p => p.Start <= date && p.End >= date && !p.IsClosed, ct))
            throw new ConflictException("FinancialPeriodNotOpen");
    }
    public async Task<IReadOnlyList<FinancialPeriod>> PeriodsAsync(CancellationToken ct) => await db.FinancialPeriods.AsNoTracking().OrderByDescending(p => p.Start).ToListAsync(ct);
    public Task<FinancialPeriod?> PeriodAsync(Guid id, CancellationToken ct) => db.FinancialPeriods.AsTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
    public async Task AddPeriodAsync(FinancialPeriod p, FinancialPeriodEvent audit, CancellationToken ct) { db.Add(p); db.Add(audit); await db.SaveChangesAsync(ct); }
    public async Task AddEventAsync(FinancialPeriodEvent audit, CancellationToken ct) { db.Add(audit); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<PeriodEventView>> EventsAsync(CancellationToken ct) => await db.FinancialPeriodEvents.AsNoTracking()
        .OrderByDescending(e => e.CreatedAt).Select(e => new PeriodEventView(e.PeriodId, e.IsClosed, e.Reason, e.ActorName, e.CreatedAt, e.Version)).ToListAsync(ct);
    public async Task<CashPosition> PositionAsync(AccountType account, DateOnly date, CancellationToken ct)
    {
        var id = Account.IdFor(account);
        var rows = await db.AccountTransactions.AsNoTracking().Where(t => t.AccountId == id && t.LedgerEntry.PostedAt != null && t.LedgerEntry.EntryDate <= date)
            .OrderBy(t => t.Id).Select(t => new { t.Id, t.Debit, t.Credit }).ToListAsync(ct);
        return new(account, date, rows.Sum(t => t.Debit - t.Credit), Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows))));
    }
    public Task<CashTransfer?> TransferAsync(Guid id, CancellationToken ct) => db.CashTransfers.AsNoTracking().SingleOrDefaultAsync(t => t.OperationId == id, ct);
    public async Task AddTransferAsync(CashTransfer t, CancellationToken ct) { db.Add(t); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<CashTransferView>> TransfersAsync(CancellationToken ct) => await db.CashTransfers.AsNoTracking().OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).Take(200)
        .Select(t => new CashTransferView(t.Date, t.FromAccount, t.ToAccount, t.Amount, t.Reason, t.ActorName)).ToListAsync(ct);
    public Task<CashReconciliation?> ReconciliationAsync(Guid id, CancellationToken ct) => db.CashReconciliations.AsNoTracking().SingleOrDefaultAsync(t => t.OperationId == id, ct);
    public async Task AddReconciliationAsync(CashReconciliation t, CancellationToken ct) { db.Add(t); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<CashReconciliation>> ReconciliationsAsync(CancellationToken ct) => await db.CashReconciliations.AsNoTracking().OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).Take(200).ToListAsync(ct);
}
