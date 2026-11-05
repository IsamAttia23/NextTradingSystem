using NEXT.Application.Exceptions;
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

    /// <summary>Builds common search conditions without executing or paging the query.</summary>
    public virtual Expression<Func<TEntity, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        return Expression.Lambda<Func<TEntity, bool>>(
            BuildSearchExpression(query.SearchTerm, parameter), parameter);
    }

    protected Expression BuildSearchExpression(string? searchTerm, ParameterExpression parameter, params string[] propertyPaths)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Expression.Constant(true);
        }
        if (propertyPaths.Length == 0)
        {
            propertyPaths = Context.Model.FindEntityType(typeof(TEntity))!.GetProperties()
                        .Where(p => p.ClrType == typeof(string) &&
                            new[] { "Name", "Code", "Phone", "Email", "Description", "Symbol", "BatchNumber", "Module" }.Contains(p.Name))
                        .Select(p => p.Name).ToArray();
        }
        Expression? search = null;
        foreach (var propertyPath in propertyPaths)
        {
            Expression member = parameter;
            Expression? guard = null;
            foreach (var name in propertyPath.Split('.'))
            {
                member = Expression.Property(member, name);
                if (!member.Type.IsValueType)
                {
                    var notNull = Expression.NotEqual(member, Expression.Constant(null, member.Type));
                    guard = guard is null ? notNull : Expression.AndAlso(guard, notNull);
                }
            }
            Expression contains = Expression.Call(member, nameof(string.Contains), Type.EmptyTypes,
                Expression.Constant(searchTerm.Trim()));
            if (guard is not null)
            {
                contains = Expression.AndAlso(guard, contains);
            }
            search = search is null ? contains : Expression.OrElse(search, contains);
        }
        return search ?? Expression.Constant(true);
    }

    protected Expression<Func<TEntity, bool>> CombineSearchExpression(
        Expression<Func<TEntity, bool>> filters, string? searchTerm, params string[] propertyPaths)
    {
        var parameter = filters.Parameters[0];
        var search = BuildSearchExpression(searchTerm, parameter, propertyPaths);
        return Expression.Lambda<Func<TEntity, bool>>(
            Expression.AndAlso(filters.Body, search), parameter);
    }

    public virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> source, string? sortBy, SortDirection? sortDirection)
    {
        ArgumentNullException.ThrowIfNull(source);
        var properties = Context.Model.FindEntityType(typeof(TEntity))!.GetProperties().Where(p => p.ClrType != typeof(byte[])).ToArray();
        var sort = properties.FirstOrDefault(p => string.Equals(p.Name, sortBy?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? properties.FirstOrDefault(p => p.Name == "Name") ?? properties.First(p => p.IsPrimaryKey());
        var ordered = OrderByField(source, sort.Name, sortDirection == SortDirection.Descending);
        foreach (var primary in properties.Where(p => p.IsPrimaryKey() && p.Name != sort.Name))
        {
            ordered = ThenByField(ordered, primary.Name);
        }
        return ordered;
    }
    protected IOrderedQueryable<TEntity> OrderByField(IQueryable<TEntity> source, string field, bool descending) =>
        OrderField(source, field, descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy));
    protected IOrderedQueryable<TEntity> ThenByField(IOrderedQueryable<TEntity> source, string field, bool descending = false) =>
        OrderField(source, field, descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));
    private IOrderedQueryable<TEntity> OrderField(IQueryable<TEntity> source, string field, string operation)
    {
        var property = Context.Model.FindEntityType(typeof(TEntity))!.FindProperty(field)
            ?? throw new ArgumentException("Unknown sort field.", nameof(field));
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var key = Expression.Call(typeof(EF), nameof(EF.Property), [property.ClrType], parameter, Expression.Constant(property.Name));
        return (IOrderedQueryable<TEntity>)source.Provider.CreateQuery<TEntity>(Expression.Call(typeof(Queryable), operation,
            [typeof(TEntity), property.ClrType], source.Expression, Expression.Quote(Expression.Lambda(key, parameter))));
    }

    public Task<TEntity?> FindTrackedAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => StorageErrors.ExecuteAsync(() => _set.FirstOrDefaultAsync(predicate, cancellationToken));

    public IQueryable<TEntity> Query(bool asNoTracking = true) => asNoTracking ? _set.AsNoTracking() : _set;

    public async Task<TEntity?> GetByKeyAsync(object[] keyValues, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyValues);
        if (keyValues.Length == 0)
        {
            throw new ArgumentException("At least one key value is required.", nameof(keyValues));
        }
        var entity = await StorageErrors.ExecuteAsync(() => _set.FindAsync(keyValues, cancellationToken).AsTask());
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
        return StorageErrors.ExecuteAsync(() => _set.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken));
    }

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return StorageErrors.ExecuteAsync(() => _set.AnyAsync(predicate, cancellationToken));
    }

    public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
        => predicate is null
            ? StorageErrors.ExecuteAsync(() => _set.CountAsync(cancellationToken))
            : StorageErrors.ExecuteAsync(() => _set.CountAsync(predicate, cancellationToken));

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


    public void SetRowVersion(TEntity entity, byte[]? rowVersion)
    {
        var entry = Context.Entry(entity);
        var property = entry.Metadata.FindProperty("RowVersion");
        if (property is not { IsConcurrencyToken: true })
        {
            return;
        }
        if (rowVersion is not { Length: > 0 })
        {
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["RowVersion"] = ["Reload the record before editing it."]
            });
        }
        var version = entry.Property<byte[]>("RowVersion");
        if (!version.CurrentValue!.SequenceEqual(rowVersion))
        {
            throw new ConflictException("The record changed after it was opened. Reload and retry.");
        }
        version.OriginalValue = rowVersion.ToArray();
    }


    public virtual async Task<bool> HasActiveRelationsAsync(TEntity entity, CancellationToken ct = default)
    {
        var entityType = Context.Model.FindEntityType(typeof(TEntity));
        if (entityType == null)
        {
            return false;
        }
        var method = typeof(TRepository<TEntity>)
            .GetMethod(nameof(AnyDependentAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var fk in entityType.GetReferencingForeignKeys())
        {
            var values = fk.PrincipalKey.Properties
                .Select(p => Context.Entry(entity).Property(p.Name).CurrentValue).ToArray();
            if (values.Any(value => value is null))
            {
                continue;
            }
            var dependentType = fk.DeclaringEntityType.ClrType;
            var task = (Task<bool>)method.MakeGenericMethod(dependentType)
                .Invoke(this, new object[] { fk.Properties, values, ct })!;
            if (await task)
            {
                return true;
            }
        }
        return false;
    }

    private Task<bool> AnyDependentAsync<TDependent>(IReadOnlyList<IProperty> foreignKeys, object[] values, CancellationToken ct)
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
        return Context.Set<TDependent>().AnyAsync(predicate, ct);
    }
}
