using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence;
namespace NEXT.Infrastructure.Persistence;
public sealed class QueryExecutor : IQueryExecutor
{
    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken ct = default) where T : class
        => query.FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken ct = default) where T : class
        => await query.ToListAsync(ct);
}
