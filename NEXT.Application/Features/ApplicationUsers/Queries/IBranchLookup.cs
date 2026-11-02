namespace NEXT.Application.Features.ApplicationUsers.Queries;

public interface IBranchLookup
{
    Task<IReadOnlyList<BranchOption>> GetAsync(CancellationToken ct = default);
}
