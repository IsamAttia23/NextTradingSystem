using Microsoft.EntityFrameworkCore;
using NEXT.Application.Features.ApplicationUsers.Queries;
using Mapster;

namespace NEXT.Infrastructure.Persistence.UsersManagement;

public sealed class BranchLookup(NEXTDbContext db, TypeAdapterConfig mapping) : IBranchLookup
{
    public async Task<IReadOnlyList<BranchOption>> GetAsync(CancellationToken ct = default) =>
        await StorageErrors.ExecuteAsync(() => db.Branches.AsNoTracking().OrderBy(x => x.Name)
            .ProjectToType<BranchOption>(mapping).ToListAsync(ct));
}
