namespace NEXT.Application.Abstractions.Persistence;

/// <summary>
/// Provides generic repositories and a single save boundary for a unit of work.
/// </summary>
public interface IUnitOfWork
{
    IRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
