using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Role. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
}
