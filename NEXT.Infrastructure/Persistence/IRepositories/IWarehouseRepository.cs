using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Warehouse. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IWarehouseRepository : IRepository<Warehouse, Guid>
{
    Task<List<Warehouse>> GetByBranchAsync(Guid? branchId, CancellationToken cancellationToken = default);
}
