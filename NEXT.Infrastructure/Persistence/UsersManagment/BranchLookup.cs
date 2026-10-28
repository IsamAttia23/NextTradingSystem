using Microsoft.EntityFrameworkCore;
using NEXT.Application.Features.ApplicationUsers.Queries;

namespace NEXT.Infrastructure.Persistence.UsersManagment;

public sealed class BranchLookup(NEXTDbContext db) : IBranchLookup
{
    public async Task<IReadOnlyList<BranchOption>> GetAsync(CancellationToken ct = default) =>
        await db.Branches.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new BranchOption(x.Id, x.Name, x.IsActive)).ToListAsync(ct);
}
