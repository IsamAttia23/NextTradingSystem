using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
namespace NEXT.Infrastructure.Persistence.Transactions;

// One boundary covers every write made by a save pipeline or application operation.
internal sealed class PersistenceTransaction(IDbContextTransaction? transaction, string? savepoint, bool ownsTransaction)
{
    public static PersistenceTransaction Begin(NEXTDbContext context, IsolationLevel? isolation = null)
    {
        if (!context.Database.IsRelational()) return new(null, null, false);
        var current = context.Database.CurrentTransaction;
        if (current is null) return new(isolation.HasValue ? context.Database.BeginTransaction(isolation.Value) : context.Database.BeginTransaction(), null, true);
        RequireSavepoints(current);
        var name = "next_" + Guid.NewGuid().ToString("N");
        current.CreateSavepoint(name);
        return new(current, name, false);
    }
    public static async Task<PersistenceTransaction> BeginAsync(NEXTDbContext context, CancellationToken ct, IsolationLevel? isolation = null)
    {
        if (!context.Database.IsRelational()) return new(null, null, false);
        var current = context.Database.CurrentTransaction;
        if (current is null) return new(isolation.HasValue ? await context.Database.BeginTransactionAsync(isolation.Value, ct) : await context.Database.BeginTransactionAsync(ct), null, true);
        RequireSavepoints(current);
        var name = "next_" + Guid.NewGuid().ToString("N");
        await current.CreateSavepointAsync(name, ct);
        return new(current, name, false);
    }
    private static void RequireSavepoints(IDbContextTransaction transaction)
    {
        if (!transaction.SupportsSavepoints) throw new InvalidOperationException("Atomic nested writes require savepoint support.");
    }
    public void Complete()
    {
        if (ownsTransaction) transaction!.Commit();
        else if (savepoint is not null) transaction!.ReleaseSavepoint(savepoint);
    }
    public async Task CompleteAsync(CancellationToken ct)
    {
        if (ownsTransaction) await transaction!.CommitAsync(ct);
        else if (savepoint is not null) await transaction!.ReleaseSavepointAsync(savepoint, ct);
    }
    public bool Rollback(Exception original, ILogger? logger = null)
    {
        try
        {
            if (ownsTransaction) transaction!.Rollback();
            else if (savepoint is not null) transaction!.RollbackToSavepoint(savepoint);
            return true;
        }
        catch (Exception cleanup) { Record(original, "RollbackFailure", cleanup, logger); return false; }
    }
    public async Task<bool> RollbackAsync(Exception original, ILogger? logger = null)
    {
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (ownsTransaction) await transaction!.RollbackAsync(cleanup.Token);
            else if (savepoint is not null) await transaction!.RollbackToSavepointAsync(savepoint, cleanup.Token);
            return true;
        }
        catch (Exception cleanup) { Record(original, "RollbackFailure", cleanup, logger); return false; }
    }
    public void Dispose(Exception? original)
    {
        try { if (ownsTransaction) transaction!.Dispose(); }
        catch (Exception cleanup) when (original is not null) { Record(original, "TransactionDisposalFailure", cleanup); }
    }
    public async Task DisposeAsync(Exception? original)
    {
        try { if (ownsTransaction) await transaction!.DisposeAsync(); }
        catch (Exception cleanup) when (original is not null) { Record(original, "TransactionDisposalFailure", cleanup); }
    }
    private static void Record(Exception original, string key, Exception cleanup, ILogger? logger = null)
    {
        original.Data[key] = cleanup;
        try { logger?.LogError(cleanup, "Transaction cleanup failed ({Stage}); preserving the operation exception.", key); }
        catch (Exception logging) { original.Data["LoggingFailure"] = logging; }
    }
}
