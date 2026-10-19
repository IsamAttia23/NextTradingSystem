using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Warehouse; inherits common CRUD behavior from the generic repository.</summary>
public sealed class WarehouseRepository(NEXTDbContext context) : Repository<Warehouse, Guid>(context), IWarehouseRepository
{
    public Task<List<Warehouse>> GetByBranchAsync(Guid? branchId, CancellationToken cancellationToken = default) => context.Warehouses.AsNoTracking().Where(x=>x.BranchId==branchId).OrderBy(x=>x.Name).ToListAsync(cancellationToken);
}
