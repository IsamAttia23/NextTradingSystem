using NEXT.Application.Common.QueryModels;
using NEXT.Application.Exceptions;

namespace NEXT.Application.Features.RecycleBin;

public interface IRecycleBinAccess
{
    IReadOnlyList<string> Sections { get; }
    void ValidateSection(string section);
    Task<TPaginationQueryModel<RecycleBinItemDto<Guid>>> ListAsync(string section, PaginationQuery query, CancellationToken ct = default);
    Task RestoreAsync(string section, Guid id, CancellationToken ct);
    Task PurgeAsync(string section, Guid id, CancellationToken ct, string? reason = null);
}
