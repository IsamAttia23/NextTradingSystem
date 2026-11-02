using NEXT.Application.Common.QueryModels;
using NEXT.Application.Exceptions;

namespace NEXT.Application.Features.RecycleBin;

public sealed class RecycleBinAccess(IEnumerable<IRecycleBinSection> sections, NEXT.Application.Abstractions.Context.IDeletionAuditContext deletionAudit) : IRecycleBinAccess
{
    public IReadOnlyList<string> Sections => sections.Select(x => x.Name).ToArray();
    private IRecycleBinSection Find(string section) => sections.SingleOrDefault(x => x.Name == section)
        ?? throw new NotFoundException("Recycle bin section was not found.");
    public void ValidateSection(string section) { _ = Find(section); }
    public Task<TPaginationQueryModel<RecycleBinItemDto<Guid>>> ListAsync(string section, PaginationQuery query, CancellationToken ct = default) => Find(section).List(query, ct);
    public Task RestoreAsync(string section, Guid id, CancellationToken ct) => Find(section).Restore(id, ct);
    public async Task PurgeAsync(string section, Guid id, CancellationToken ct, string? reason = null)
    {
        using var audit = deletionAudit.Begin(NEXT.Application.Services.DeletionReason.Require(reason));
        await Find(section).Purge(id, ct);
    }
}
