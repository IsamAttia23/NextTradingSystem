using System.Linq.Expressions;
using NEXT.Domain.Common.Entities;

namespace NEXT.Infrastructure.Persistence.IRepositories;

/// <summary>
/// Generic repository contract. The TKey type matches the entity's primary key type.
/// Use GetByKeyAsync for entities with composite primary keys.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query(bool asNoTracking = true);

    Task<TEntity?> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default);

    IQueryable<TEntity> GetAllAsync(CancellationToken cancellationToken = default);

    IQueryable<TEntity> FindAsync( Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync( Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate,CancellationToken cancellationToken = default);

    Task<int> CountAsync( Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    Task Update(TEntity entity);

    void SetRowVersion<Entity>(Entity entity, byte[] rowVersion) where Entity : class, IRowVersionProperty;

    Task<bool> HasActiveRelationsAsync(TEntity entity);
}
