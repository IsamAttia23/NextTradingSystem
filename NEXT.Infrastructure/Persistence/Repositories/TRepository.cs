using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;
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

    /// <summary>Common search for entities without a specialized query model.</summary>
    public virtual IQueryable<TEntity> Search(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = Query();
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            Expression? predicate = null;
            foreach (var property in Context.Model.FindEntityType(typeof(TEntity))!.GetProperties().Where(p => p.ClrType == typeof(string) &&
                new[] { "Name", "Code", "Phone", "Email", "Description", "Symbol", "BatchNumber", "Module" }.Contains(p.Name)))
            {
                var member = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(string)], parameter, Expression.Constant(property.Name));
                var contains = Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
                    Expression.Call(member, nameof(string.Contains), Type.EmptyTypes, Expression.Constant(query.SearchTerm.Trim())));
                predicate = predicate is null ? contains : Expression.OrElse(predicate, contains);
            }
            if (predicate is not null) source = source.Where(Expression.Lambda<Func<TEntity, bool>>(predicate, parameter));
        }
        var properties = Context.Model.FindEntityType(typeof(TEntity))!.GetProperties().Where(p => p.ClrType != typeof(byte[])).ToArray();
        var sort = properties.FirstOrDefault(p => string.Equals(p.Name, query.SortBy?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? properties.FirstOrDefault(p => p.Name == "Name") ?? properties.First(p => p.IsPrimaryKey());
        var key = Expression.Call(typeof(EF), nameof(EF.Property), [sort.ClrType], parameter, Expression.Constant(sort.Name));
        var ordered = source.Provider.CreateQuery<TEntity>(Expression.Call(typeof(Queryable),
            query.SortDirection == SortDirection.Descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy),
            [typeof(TEntity), sort.ClrType], source.Expression, Expression.Quote(Expression.Lambda(key, parameter))));
        // A unique tie-breaker keeps paging stable for duplicate names.
        foreach (var primary in properties.Where(p => p.IsPrimaryKey() && p.Name != sort.Name))
        {
            var member = Expression.Call(typeof(EF), nameof(EF.Property), [primary.ClrType], parameter, Expression.Constant(primary.Name));
            ordered = ordered.Provider.CreateQuery<TEntity>(Expression.Call(typeof(Queryable), nameof(Queryable.ThenBy),
                [typeof(TEntity), primary.ClrType], ordered.Expression, Expression.Quote(Expression.Lambda(member, parameter))));
        }
        return ordered;
    }

    public Task<TEntity?> FindTrackedAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => _set.FirstOrDefaultAsync(predicate, cancellationToken);

    public IQueryable<TEntity> Query(bool asNoTracking = true) => asNoTracking ? _set.AsNoTracking() : _set;

    public async Task<TEntity?> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyValues);
        if (keyValues.Length == 0)
            throw new ArgumentException("At least one key value is required.", nameof(keyValues));

        var entity = await _set.FindAsync(keyValues, cancellationToken);
        // FindAsync can return a tracked instance without executing the soft-delete query filter.
        return entity is IIsDeletableEntity { DeletedAt: not null } ? null : entity;
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
