using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NEXT.Infrastructure.Persistence.IRepositories;
using System.Linq.Expressions;
using System.Reflection;

namespace NEXT.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation shared by all entity types; no per-entity repository classes are required.
/// </summary>
public class TRepository<TEntity, TKey>(NEXTDbContext context) : IRepository<TEntity, TKey>
    where TEntity : class
{
    private readonly DbSet<TEntity> _set = context.Set<TEntity>();

    public IQueryable<TEntity> Query(bool asNoTracking = true) => asNoTracking ? _set.AsNoTracking() : _set;

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

    public IQueryable<TEntity> GetAllAsync(CancellationToken cancellationToken = default)
        => _set.AsNoTracking().AsQueryable();

    public IQueryable<TEntity> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _set.AsNoTracking().Where(predicate);
    }

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _set.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _set.AnyAsync(predicate, cancellationToken);
    }

    public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
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

    void IRepository<TEntity, TKey>.SetRowVersion<Entity>(Entity entity, byte[] rowVersion)
    {
        context.Entry(entity).Property(e => e.RowVersion).OriginalValue = rowVersion;
    }

    public async Task<bool> HasActiveRelationsAsync(TEntity entity)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        if (entityType == null)
            return false;

        var method = typeof(TRepository<TEntity, TKey>)
            .GetMethod(nameof(AnyDependentAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var fk in entityType.GetReferencingForeignKeys())
        {
            var principalKey = fk.PrincipalKey.Properties[0];
            var foreignKey = fk.Properties[0];

            var id = context.Entry(entity).Property(principalKey.Name).CurrentValue;
            if (id == null)
                continue;

            var dependentType = fk.DeclaringEntityType.ClrType;
            var hasDeletedOn = fk.DeclaringEntityType.FindProperty("DeletedOn") != null;

            var task = (Task<bool>)method
                .MakeGenericMethod(dependentType)
                .Invoke(this, new object[] { foreignKey, id, hasDeletedOn })!;

            if (await task)
                return true;
        }

        return false;
    }

    private async Task<bool> AnyDependentAsync<TDependent>( IProperty foreignKey, object id, bool hasDeletedOn) where TDependent : class
    {
        var param = Expression.Parameter(typeof(TDependent), "x");

        // EF.Property<TFk>(x, "ForeignKeyName") == id
        var fkAccess = Expression.Call(
            typeof(EF), nameof(EF.Property), new[] { foreignKey.ClrType },
            param, Expression.Constant(foreignKey.Name));

        Expression body = Expression.Equal(fkAccess, Expression.Constant(id, foreignKey.ClrType));

        // && EF.Property<DateTimeOffset?>(x, "DeletedOn") == null
        if (hasDeletedOn)
        {
            var deletedOn = Expression.Call(
                typeof(EF), nameof(EF.Property), new[] { typeof(DateTimeOffset?) },
                param, Expression.Constant("DeletedOn"));

            body = Expression.AndAlso(
                body,
                Expression.Equal(deletedOn, Expression.Constant(null, typeof(DateTimeOffset?))));
        }

        var lambda = Expression.Lambda<Func<TDependent, bool>>(body, param);
        return await context.Set<TDependent>().AnyAsync(lambda);
    }
}
