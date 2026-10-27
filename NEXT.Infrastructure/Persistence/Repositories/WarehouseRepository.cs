using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Warehouse; inherits common CRUD behavior from the generic repository.</summary>
public sealed class WarehouseRepository(NEXTDbContext context) : KeyRepository<Warehouse, Guid>(context), IWarehouseRepository
{
    public IQueryable<Warehouse> GetByBranchAsync(Guid? branchId)
        => Context.Warehouses.AsNoTracking().Where(x=>x.BranchId==branchId).OrderBy(x=>x.Name).AsQueryable();
}
