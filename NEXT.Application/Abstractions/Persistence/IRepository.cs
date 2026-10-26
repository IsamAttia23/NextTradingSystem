using System.Linq.Expressions;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Abstractions.Persistence;

/// <summary>Persistence contract consumed by the application layer.</summary>
public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query(bool asNoTracking = true);
    Task<TEntity?> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default);
    IQueryable<TEntity> GetAllAsync(CancellationToken cancellationToken = default);
    IQueryable<TEntity> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    Task Update(TEntity entity);
    void SetRowVersion<TEntityWithVersion>(TEntityWithVersion entity, byte[] rowVersion) where TEntityWithVersion : class, IRowVersionProperty;
    Task<bool> HasActiveRelationsAsync(TEntity entity);
}
