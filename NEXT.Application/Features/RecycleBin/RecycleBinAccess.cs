using NEXT.Application.Common.QueryModels;
using NEXT.Application.Exceptions;
namespace NEXT.Application.Features.RecycleBin;
public interface IRecycleBinAccess
{
    IReadOnlyList<string> Sections { get; }
    void ValidateSection(string section);
    Task<TPaginationQueryModel<RecycleBinItemDto<Guid>>> ListAsync(string section, PaginationQuery query);
    Task RestoreAsync(string section, Guid id, CancellationToken ct);
    Task PurgeAsync(string section, Guid id, CancellationToken ct, string? reason = null);
}
public sealed class RecycleBinAccess(IEnumerable<IRecycleBinSection> sections, NEXT.Application.Abstractions.Context.IDeletionAuditContext deletionAudit) : IRecycleBinAccess
{
    public IReadOnlyList<string> Sections => sections.Select(x => x.Name).ToArray();
    private IRecycleBinSection Find(string section) => sections.SingleOrDefault(x => x.Name == section)
        ?? throw new NotFoundException("Recycle bin section was not found.");
    public void ValidateSection(string section) { _ = Find(section); }
    public Task<TPaginationQueryModel<RecycleBinItemDto<Guid>>> ListAsync(string section, PaginationQuery query) => Find(section).List(query);
    public Task RestoreAsync(string section, Guid id, CancellationToken ct) => Find(section).Restore(id, ct);
    public async Task PurgeAsync(string section, Guid id, CancellationToken ct, string? reason = null)
    {
        using var audit = deletionAudit.Begin(NEXT.Application.Services.DeletionReason.Require(reason));
        await Find(section).Purge(id, ct);
    }
}
