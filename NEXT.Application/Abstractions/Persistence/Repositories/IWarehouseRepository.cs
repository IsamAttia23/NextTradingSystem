using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Warehouse. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IWarehouseRepository : IKeyRepository<Warehouse, Guid>
{
    IQueryable<Warehouse> GetByBranchAsync(Guid? branchId);
}
