using NEXT.Application.Common.QueryModels;
using NEXT.Application.Features.RecycleBin;
using NEXT.Domain.Common.Entities;
namespace NEXT.Application.Abstractions.Persistence;

public interface IRecycleBinStore<TEntity, TKey> where TKey : struct where TEntity : IsDeletableEntity<TKey>
{
    Task<TPaginationQueryModel<RecycleBinItemDto<TKey>>> List(PaginationQuery query, CancellationToken ct);
    Task<TEntity?> FindDeletedAsync(TKey id, CancellationToken ct);
    Task PurgeAsync(TEntity entity, CancellationToken ct);
}
