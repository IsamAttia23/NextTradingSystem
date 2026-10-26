using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for UserRole. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IUserRoleRepository : IRepository<UserRole>
{

}
