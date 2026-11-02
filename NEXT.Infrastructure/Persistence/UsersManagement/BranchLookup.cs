using Microsoft.EntityFrameworkCore;
using NEXT.Application.Features.ApplicationUsers.Queries;

namespace NEXT.Infrastructure.Persistence.UsersManagement;

public sealed class BranchLookup(NEXTDbContext db) : IBranchLookup
{
    public async Task<IReadOnlyList<BranchOption>> GetAsync(CancellationToken ct = default) =>
        await StorageErrors.ExecuteAsync(() => db.Branches.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new BranchOption(x.Id, x.Name, x.IsActive)).ToListAsync(ct));
}
