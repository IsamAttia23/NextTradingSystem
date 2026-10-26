using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Branch. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IBranchRepository : IKeyRepository<Branch, Guid>
{

}
