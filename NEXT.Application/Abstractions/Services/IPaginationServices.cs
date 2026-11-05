using NEXT.Application.Common.QueryModels;

namespace NEXT.Application.Abstractions.Services;

public interface IPaginationServices
{
    Task<TPaginationQueryModel<T>> GetPagedList<T>(IQueryable<T> entities, PaginationQuery query, CancellationToken cancellationToken = default) where T : class;
    Task<TPaginationQueryModel<TResult>> GetPagedList<TEntity, TResult>(IQueryable<TEntity> entities,
        Func<IQueryable<TEntity>, IQueryable<TResult>> projection, PaginationQuery query, CancellationToken cancellationToken = default)
        where TEntity : class where TResult : class;
}
