using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Permission. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IPermissionRepository : IKeyRepository<Permission, Guid>
{

}
