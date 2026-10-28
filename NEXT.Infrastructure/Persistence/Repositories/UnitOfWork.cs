using System.Data;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Exceptions;
using Npgsql;
namespace NEXT.Infrastructure.Persistence.Repositories;
public sealed class UnitOfWork(NEXTDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { return await context.SaveChangesAsync(cancellationToken); }
        catch (Exception ex) when (IsConflict(ex))
        { throw new ConflictException("The record or inventory changed concurrently, or a unique key already exists. Reload and retry."); }
    }
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (context.Database.CurrentTransaction is not null)
        {
            var nested = await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            return nested;
        }
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            if (IsConflict(ex)) throw new ConflictException("The record or inventory changed concurrently. Reload and retry.");
            throw;
        }
    }
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await ExecuteInTransactionAsync(async ct => { await operation(ct); return true; }, cancellationToken);
    }
    // Npgsql's execution strategy can wrap the PostgreSQL error in InvalidOperationException.
    private static bool IsConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: "23505" or "40001" or "40P01" }) return true;
        return false;
    }
}
