using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for AppUser. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IAppUserRepository : IRepository<ApplicationUser, Guid>
{
    Task<ApplicationUser?> GetByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default);
}
