using NEXT.Domain.Common.Entities;
using NEXT.Application.Common.QueryModels;

namespace NEXT.Application.Features.RecycleBin;

public interface IRecycleBinService<TEntity, TKey>
    where TKey : struct
    where TEntity : IsDeletableEntity<TKey>
{
    Task<TPaginationQueryModel<RecycleBinItemDto<TKey>>> List(PaginationQuery query);
    Task HardDelete(TKey id, CancellationToken ct = default);
    Task Restore(TKey id, CancellationToken ct = default);
}
