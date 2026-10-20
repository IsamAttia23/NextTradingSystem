using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Role. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IRoleRepository : IRepository<Role, Guid>
{
    Task<Role?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
}
