using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Role; inherits common CRUD behavior from the generic repository.</summary>
public sealed class RoleRepository(NEXTDbContext context) : TRepository<Role>(context), IRoleRepository
{
    public Task<Role?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
        => context.Roles.AsNoTracking().FirstOrDefaultAsync(x=>x.NormalizedName==normalizedName,cancellationToken);
}
