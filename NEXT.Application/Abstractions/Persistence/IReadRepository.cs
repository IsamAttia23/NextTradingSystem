using System.Linq.Expressions;
using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Application.Abstractions.Persistence;

public interface IReadRepository<TEntity> where TEntity : class
{
    Expression<Func<TEntity, bool>> BuildFilterExpression(PaginationQuery query);
    IQueryable<TEntity> ApplySorting(IQueryable<TEntity> entities, string? sortBy, SortDirection? sortDirection);
    IQueryable<TEntity> Query(Expression<Func<TEntity, bool>> predicate);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
}
