using System.Linq.Expressions;

namespace NEXT.Application.Abstractions.Persistence;

public interface IRepository<TEntity> : IReadRepository<TEntity> where TEntity : class
{
    Task<TEntity?> FindTrackedAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    IQueryable<TEntity> Query(bool asNoTracking = true);
    Task<TEntity?> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    Task Update(TEntity entity);
    void SetRowVersion(TEntity entity, byte[]? rowVersion);
    Task<bool> HasActiveRelationsAsync(TEntity entity, CancellationToken ct = default);
}
