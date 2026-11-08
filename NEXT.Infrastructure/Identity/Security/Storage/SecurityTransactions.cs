using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Transactions;

namespace NEXT.Infrastructure.Identity.Security.Storage;

public sealed class SecurityTransactions(NEXTDbContext db)
{
    private readonly HashSet<string> heldKeys = [];
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TestLocks = new();

    public async Task<T> RunAsync<T>(string key, Func<Task<T>> action, CancellationToken ct)
    {
        // Nested services share this scoped context and its existing lock/transaction.
        if (heldKeys.Contains(key)) return await action();
        heldKeys.Add(key);
        try { return await RunLockedAsync(key, action, ct); }
        finally { heldKeys.Remove(key); }
    }

    private async Task<T> RunLockedAsync<T>(string key, Func<Task<T>> action, CancellationToken ct)
    {
        // The fallback is for the EF in-memory test provider only.
        if (!db.Database.IsRelational())
        {
            var gate = TestLocks.GetOrAdd(key, _ => new SemaphoreSlim(1));
            await gate.WaitAsync(ct);
            try { return await action(); }
            finally { gate.Release(); }
        }

        var transaction = await PersistenceTransaction.BeginAsync(db, ct);
        Exception? failure = null;
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            var result = await action();
            await transaction.CompleteAsync(ct);
            return result;
        }
        catch (Exception error)
        {
            failure = error;
            await transaction.RollbackAsync(error);
            db.ChangeTracker.Clear();
            StorageErrors.ThrowTranslated(error);
            throw;
        }
        finally { await transaction.DisposeAsync(failure); }
    }
}
