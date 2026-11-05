using NEXT.Application.Abstractions.Persistence;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.Exceptions;
using NEXT.Domain.Common.Entities;
namespace NEXT.Application.Features.RecycleBin;

public sealed class RecycleBinService<TEntity, TKey>(IRecycleBinStore<TEntity, TKey> store, IUnitOfWork unitOfWork,
    IEnumerable<IRecycleBinHandler<TEntity, TKey>> handlers) : IRecycleBinService<TEntity, TKey>
    where TKey : struct where TEntity : IsDeletableEntity<TKey>
{
    public Task<TPaginationQueryModel<RecycleBinItemDto<TKey>>> List(PaginationQuery query, CancellationToken ct = default) => store.List(query, ct);
    public Task HardDelete(TKey id, CancellationToken ct = default) => unitOfWork.ExecuteInTransactionAsync(async token =>
    {
        var entity = await RequireAsync(id, token);
        foreach (var handler in handlers)
        {
            await handler.OnHardDeleteAsync(entity, token);
        }
        await store.PurgeAsync(entity, token);
    }, ct);
    public Task Restore(TKey id, CancellationToken ct = default) => unitOfWork.ExecuteInTransactionAsync(async token =>
    {
        var entity = await RequireAsync(id, token);
        foreach (var handler in handlers)
        {
            await handler.OnRestoreAsync(entity, token);
        }
        entity.DeletedAt = null;
        entity.DeletedBy = null;
        entity.DeletionReason = null;
    }, ct);
    private async Task<TEntity> RequireAsync(TKey id, CancellationToken ct) => await store.FindDeletedAsync(id, ct)
        ?? throw new NotFoundException("Deleted entity was not found.");
}
