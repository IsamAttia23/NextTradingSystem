using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for StockMovement; inherits common CRUD behavior from the generic repository.</summary>
public sealed class StockMovementRepository(NEXTDbContext context) : Repository<StockMovement, Guid>(context), IStockMovementRepository
{
    public Task<List<StockMovement>> GetForProductAsync(Guid productId, Guid? warehouseId, DateTimeOffset? from, DateTimeOffset? to, int skip, int take, CancellationToken cancellationToken = default)
    {
        var q = context.StockMovements.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .Where(x => x.ProductId == productId);
        if (warehouseId.HasValue) q = q.Where(x => x.WarehouseId == warehouseId);
        if (from.HasValue) q = q.Where(x => x.CreatedAt >= from);
        if (to.HasValue) q = q.Where(x => x.CreatedAt < to);
        return q.OrderByDescending(x => x.CreatedAt).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
    }
}
