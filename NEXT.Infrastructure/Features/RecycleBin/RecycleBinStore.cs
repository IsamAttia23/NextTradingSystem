using Microsoft.EntityFrameworkCore;
using NEXT.Application.Features.RecycleBin;
using NEXT.Application.Exceptions;
using NEXT.Application.Abstractions.Services;
using NEXT.Domain.Common.Entities;
using NEXT.Application.Common.QueryModels;
using NEXT.Infrastructure.Persistence;
using Mapster;

namespace NEXT.Infrastructure.Features.RecycleBin;

// A closed generic service handles one entity type; it cannot cast an unrelated runtime type.
public sealed class RecycleBinStore<TEntity, TKey>(
    NEXTDbContext db,
    IPaginationServices pagination, TypeAdapterConfig mapping) : NEXT.Application.Abstractions.Persistence.IRecycleBinStore<TEntity, TKey>
    where TKey : struct
    where TEntity : IsDeletableEntity<TKey>
{
    public async Task<TPaginationQueryModel<RecycleBinItemDto<TKey>>> List(PaginationQuery query, CancellationToken ct = default)
    {
        var deleted = db.Set<TEntity>().IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.DeletedAt != null)
            .OrderByDescending(x => x.DeletedAt).ThenBy(x => x.Id);
        var result = await pagination.GetPagedList(deleted,
            source => source.ProjectToType<RecycleBinItemDto<TKey>>(mapping), query, ct);
        foreach (var item in result.Data) item.EntityType = typeof(TEntity).Name;
        return result;
    }

    public Task<TEntity?> FindDeletedAsync(TKey id, CancellationToken ct) => StorageErrors.ExecuteAsync(() => db.Set<TEntity>().IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id.Equals(id) && x.DeletedAt != null, ct));
    public Task PurgeAsync(TEntity entity, CancellationToken ct) => db.PermanentlyDeleteAsync(entity, ct);
}
