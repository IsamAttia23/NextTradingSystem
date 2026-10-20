using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for PurchaseOrder; inherits common CRUD behavior from the generic repository.</summary>
public sealed class PurchaseOrderRepository(NEXTDbContext context) : TRepository<PurchaseOrder, Guid>(context), IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => context.PurchaseOrders.AsNoTracking().Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(i => i.Product).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<List<PurchaseOrder>> SearchAsync(DateOnly? from, DateOnly? to, Guid? supplierId, PurchaseOrderStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var q = context.PurchaseOrders.AsNoTracking().Include(x => x.Supplier).AsQueryable();
        if (from.HasValue) q = q.Where(x => x.OrderDate >= from);
        if (to.HasValue) q = q.Where(x => x.OrderDate <= to);
        if (supplierId.HasValue) q = q.Where(x => x.SupplierId == supplierId);
        if (status.HasValue) q = q.Where(x => x.Status == status);
        return q.OrderByDescending(x => x.OrderDate).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
    }
}
