using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
}
