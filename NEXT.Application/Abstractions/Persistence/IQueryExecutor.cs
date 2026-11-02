namespace NEXT.Application.Abstractions.Persistence;

public interface IQueryExecutor
{
    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) where T : class;
    Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default) where T : class;
}
