using System.Data;
using Microsoft.Extensions.Logging;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Infrastructure.Persistence.SaveChanges;
using NEXT.Infrastructure.Persistence.Transactions;
namespace NEXT.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork(NEXTDbContext context, ILogger<UnitOfWork>? logger = null) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        context.EnsureTransactionUsable();
        ct.ThrowIfCancellationRequested();
        var state = new SaveChangesStatePreserver(context);
        var before = state.Capture();
        PersistenceTransaction? transaction = null;
        Exception? failure = null;
        try
        {
            transaction = await PersistenceTransaction.BeginAsync(context, ct, IsolationLevel.Serializable);
            var result = await operation(ct);
            await SaveChangesAsync(ct);
            context.EnsureTransactionUsable();
            await transaction.CompleteAsync(ct);
            return result;
        }
        catch (Exception error)
        {
            failure = error;
            if (transaction is not null && !await transaction.RollbackAsync(error, logger)) context.TransactionFailure = error;
            try { state.Restore(before); }
            catch (Exception cleanup) { error.Data["TrackerRestoreFailure"] = cleanup; context.TransactionFailure = error; }
            StorageErrors.ThrowTranslated(error);
            throw;
        }
        finally { if (transaction is not null) await transaction.DisposeAsync(failure); }
    }
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default) =>
        await ExecuteInTransactionAsync(async token => { await operation(token); return true; }, ct);
}
