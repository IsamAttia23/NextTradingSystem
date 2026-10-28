using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.RecycleBin;

/// <summary>A registered, allow-listed recycle bin section. No runtime entity reflection is needed.</summary>
public interface IRecycleBinSection
{
    string Name { get; }
    Task<TPaginationQueryModel<RecycleBinItemDto<Guid>>> List(PaginationQuery query);
    Task Restore(Guid id, CancellationToken ct);
    Task Purge(Guid id, CancellationToken ct);
}

public sealed class RecycleBinSection<TEntity>(IRecycleBinService<TEntity, Guid> service) : IRecycleBinSection
    where TEntity : IsDeletableEntity<Guid>
{
    public string Name => typeof(TEntity).Name;
    public Task<TPaginationQueryModel<RecycleBinItemDto<Guid>>> List(PaginationQuery query) => service.List(query);
    public Task Restore(Guid id, CancellationToken ct) => service.Restore(id, ct);
    public Task Purge(Guid id, CancellationToken ct) => service.HardDelete(id, ct);
}
