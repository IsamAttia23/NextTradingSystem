using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.Features.RecycleBin;

public interface IRecycleBinSection
{
    string Name { get; }
    Task<TPaginationQueryModel<RecycleBinItemDto<Guid>>> List(PaginationQuery query, CancellationToken ct = default);
    Task Restore(Guid id, CancellationToken ct);
    Task Purge(Guid id, CancellationToken ct);
}
