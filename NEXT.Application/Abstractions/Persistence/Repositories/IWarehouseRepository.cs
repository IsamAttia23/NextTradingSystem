using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IWarehouseRepository : IKeyRepository<Warehouse, Guid>
{
    IQueryable<Warehouse> GetByBranchAsync(Guid? branchId);
}
