using NEXT.Application.Common.QueryModels;

namespace NEXT.Application.Abstractions.Services
{
    public interface IPaginationServices
    {
        Task<TPaginationQueryModel<T>> GetPaggedList<T>(IQueryable<T> entities, PaginationQuery query, CancellationToken cancellationToken = default) where T : class;
    }
}
