namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed record BranchOption(Guid Id, string Name, bool IsActive);
public interface IBranchLookup
{
    Task<IReadOnlyList<BranchOption>> GetAsync(CancellationToken ct = default);
}
