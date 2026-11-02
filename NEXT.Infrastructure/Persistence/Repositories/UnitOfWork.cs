using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using Npgsql;
namespace NEXT.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork(NEXTDbContext context, ILogger<UnitOfWork>? logger = null) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try { return await context.SaveChangesAsync(ct); }
        catch (Exception error) { ThrowTranslated(error); throw; }
    }
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ct.ThrowIfCancellationRequested();
        // InMemory is only a test provider: it has no relational transaction/rollback semantics.
        if (!context.Database.IsRelational())
        {
            try { var value = await operation(ct); await SaveChangesAsync(ct); return value; }
            catch { context.ChangeTracker.Clear(); throw; }
        }
        if (context.Database.CurrentTransaction is not null)
            return await ExecuteNestedAsync(context.Database.CurrentTransaction, operation, ct);
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction;
        try { transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); }
        catch (Exception error) { ThrowTranslated(error); throw; }
        Exception? operationError = null;
        try
        {
            try
            {
                var result = await operation(ct);
                await SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (Exception error)
            {
                operationError = error;
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await transaction.RollbackAsync(cleanup.Token);
                }
                catch (Exception rollbackError)
                {
                    logger?.LogError(rollbackError, "Transaction rollback failed; preserving the original operation exception.");
                    error.Data["RollbackFailure"] = rollbackError;
                }
                context.ChangeTracker.Clear();
                ThrowTranslated(error);
                throw;
            }
        }
        finally
        {
            try { await transaction.DisposeAsync(); }
            catch (Exception disposalError) when (operationError is not null)
            {
                logger?.LogError(disposalError, "Transaction disposal failed; preserving the original operation exception.");
                operationError.Data["TransactionDisposalFailure"] = disposalError;
            }
        }
    }
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default) =>
        await ExecuteInTransactionAsync(async token => { await operation(token); return true; }, ct);
    private async Task<TResult> ExecuteNestedAsync<TResult>(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Func<CancellationToken, Task<TResult>> operation, CancellationToken ct)
    {
        if (!transaction.SupportsSavepoints)
            throw new InvalidOperationException("Nested writes require transaction savepoint support.");
        var savepoint = "app_" + Guid.NewGuid().ToString("N");
        await transaction.CreateSavepointAsync(savepoint, ct);
        try
        {
            var result = await operation(ct);
            await SaveChangesAsync(ct);
            await transaction.ReleaseSavepointAsync(savepoint, ct);
            return result;
        }
        catch (Exception error)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await transaction.RollbackToSavepointAsync(savepoint, cleanup.Token);
            }
            catch (Exception rollbackError)
            {
                // Never let a caller turn a failed partial rollback into a successful outer commit.
                throw new InvalidOperationException("Nested transaction rollback failed.", new AggregateException(error, rollbackError));
            }
            finally { context.ChangeTracker.Clear(); }
            ThrowTranslated(error);
            throw;
        }
    }
    private static void ThrowTranslated(Exception error)
    {
        if (error is AppException or OperationCanceledException) return;
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is OperationCanceledException cancellation)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cancellation).Throw();
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: "23505" or "40001" or "40P01" })
                throw new ConflictException("The record changed concurrently or a unique key already exists. Reload and retry.", error);
            if (current is PostgresException { SqlState: "23503" or "23514" or "22003" })
                throw new UnprocessableEntityException("The operation violates a record relationship or storage constraint.", error);
            if (current is TimeoutException or NpgsqlException { IsTransient: true })
                throw new DependencyUnavailableException("The data service is temporarily unavailable.", error);
        }
    }
}
