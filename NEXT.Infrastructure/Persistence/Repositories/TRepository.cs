using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NEXT.Domain.Common.Entities;
using NEXT.Application.Abstractions.Persistence;
using System.Linq.Expressions;
using System.Reflection;

namespace NEXT.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation shared by all entity types; no per-entity repository classes are required.
/// </summary>
public class TRepository<TEntity>(NEXTDbContext context) : IRepository<TEntity>
    where TEntity : class
{
    protected NEXTDbContext Context { get; } = context;
    private readonly DbSet<TEntity> _set = context.Set<TEntity>();

    public IQueryable<TEntity> Query(bool asNoTracking = true) => asNoTracking ? _set.AsNoTracking() : _set;

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

    public virtual Task Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _set.Update(entity);
        return Task.CompletedTask;
    }


    public void SetRowVersion<Entity>(Entity entity, byte[] rowVersion) where Entity : class, IRowVersionProperty
    {
        Context.Entry(entity).Property(e => e.RowVersion).OriginalValue = rowVersion;
    }


    public virtual async Task<bool> HasActiveRelationsAsync(TEntity entity)
    {
        var entityType = Context.Model.FindEntityType(typeof(TEntity));
        if (entityType == null)
            return false;

        var method = typeof(TRepository<TEntity>)
            .GetMethod(nameof(AnyDependentAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var fk in entityType.GetReferencingForeignKeys())
        {
            var values = fk.PrincipalKey.Properties
                .Select(p => Context.Entry(entity).Property(p.Name).CurrentValue).ToArray();
            if (values.Any(value => value is null))
                continue;
            var dependentType = fk.DeclaringEntityType.ClrType;
            var task = (Task<bool>)method.MakeGenericMethod(dependentType)
                .Invoke(this, new object[] { fk.Properties, values })!;

            if (await task)
                return true;
        }

        return false;
    }

    private Task<bool> AnyDependentAsync<TDependent>(IReadOnlyList<IProperty> foreignKeys, object[] values)
        where TDependent : class
    {
        var parameter = Expression.Parameter(typeof(TDependent), "entity");
        Expression body = Expression.Constant(true);
        for (var index = 0; index < foreignKeys.Count; index++)
        {
            var property = foreignKeys[index];
            var access = Expression.Call(typeof(EF), nameof(EF.Property), [property.ClrType],
                parameter, Expression.Constant(property.Name));
            var value = Expression.Convert(Expression.Constant(values[index]), property.ClrType);
            body = Expression.AndAlso(body, Expression.Equal(access, value));
        }
        // Global query filters already exclude soft-deleted dependents, including DeletedAt.
        var predicate = Expression.Lambda<Func<TDependent, bool>>(body, parameter);
        return Context.Set<TDependent>().AnyAsync(predicate);
    }
}
