using System.Linq.Expressions;

namespace NEXT.Infrastructure.Persistence.IRepositories;

/// <summary>
/// Generic repository contract. The TKey type matches the entity's primary key type.
/// Use GetByKeyAsync for entities with composite primary keys.
/// </summary>
public interface IRepository<TEntity, in TKey> where TEntity : class
{
    IQueryable<TEntity> Query(bool asNoTracking = true);

    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    Task<TEntity?> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default);

    Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<List<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);

    void RemoveRange(IEnumerable<TEntity> entities);
}
