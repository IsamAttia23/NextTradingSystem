using Microsoft.EntityFrameworkCore;
using NEXT.Application.Features.RecycleBin;
using NEXT.Application.Exceptions;
using NEXT.Application.Abstractions.Services;
using NEXT.Domain.Common.Entities;
using NEXT.Application.Common.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Features.RecycleBin;

// A closed generic service handles one entity type; it cannot cast an unrelated runtime type.
public sealed class RecycleBinService<TEntity, TKey>(
    NEXTDbContext db,
    IPaginationServices pagination,
    IEnumerable<IRecycleBinHandler<TEntity, TKey>> handlers) : IRecycleBinService<TEntity, TKey>
    where TKey : struct
    where TEntity : IsDeletableEntity<TKey>
{
    public Task<TPaginationQueryModel<RecycleBinItemDto<TKey>>> List(PaginationQuery query)
    {
        var deleted = db.Set<TEntity>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.DeletedAt != null)
            .OrderByDescending(x => x.DeletedAt).ThenBy(x => x.Id)
            .Select(x => new RecycleBinItemDto<TKey>
            {
                EntityType = typeof(TEntity).Name,
                EntityId = x.Id,
                DeletedOn = x.DeletedAt!.Value
            });
        return pagination.GetPaggedList(deleted, query);
    }

    public async Task HardDelete(TKey id, CancellationToken ct = default)
    {
        var entity = await db.Set<TEntity>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id.Equals(id) && x.DeletedAt != null, ct)
            ?? throw new NotFoundException("Deleted entity was not found.");
        foreach (var handler in handlers)
            await handler.OnHardDeleteAsync(entity, ct);
        await db.PermanentlyDeleteAsync(entity, ct);
    }

    public async Task Restore(TKey id, CancellationToken ct = default)
    {
        var entity = await db.Set<TEntity>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id.Equals(id) && x.DeletedAt != null, ct)
            ?? throw new NotFoundException("Deleted entity was not found.");
        foreach (var handler in handlers)
            await handler.OnRestoreAsync(entity, ct);
        entity.DeletedAt = null;
        entity.DeletedBy = null;
        entity.DeletionReason = null;
        await db.SaveChangesAsync(ct);
    }
}
