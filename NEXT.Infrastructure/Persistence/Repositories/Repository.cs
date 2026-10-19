using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation shared by all entity types; no per-entity repository classes are required.
/// </summary>
public class Repository<TEntity, TKey>(NEXTDbContext context) : IRepository<TEntity, TKey>
    where TEntity : class
{
    private readonly DbSet<TEntity> _set = context.Set<TEntity>();

    public IQueryable<TEntity> Query(bool asNoTracking = true)
        => asNoTracking ? _set.AsNoTracking() : _set;

    public async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        object?[] keys = id is object?[] compositeKeys ? compositeKeys : [id];
        return await _set.FindAsync(keys, cancellationToken);
    }

    public async Task<TEntity?> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyValues);
        if (keyValues.Length == 0)
            throw new ArgumentException("At least one key value is required.", nameof(keyValues));

        return await _set.FindAsync(keyValues, cancellationToken);
    }

    public Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => _set.AsNoTracking().ToListAsync(cancellationToken);

    public Task<List<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _set.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);
    }

    public Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _set.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _set.AnyAsync(predicate, cancellationToken);
    }

    public Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
        => predicate is null
            ? _set.CountAsync(cancellationToken)
            : _set.CountAsync(predicate, cancellationToken);

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _set.AddAsync(entity, cancellationToken).AsTask();
    }

    public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return _set.AddRangeAsync(entities, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _set.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        // NEXTDbContext converts deletes of SoftDeletableEntity instances into soft deletes.
        _set.Remove(entity);
    }

    public void RemoveRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _set.RemoveRange(entities);
    }
}
