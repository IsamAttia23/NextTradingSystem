using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for AppUser; inherits common CRUD behavior from the generic repository.</summary>
public sealed class ApplicationUserRepository(NEXTDbContext context) : TRepository<ApplicationUser, Guid>(context), IAppUserRepository
{
    public Task<ApplicationUser?> GetByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
        => context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken);
}
