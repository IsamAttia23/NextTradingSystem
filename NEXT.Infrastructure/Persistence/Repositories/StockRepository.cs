using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Stock; inherits common CRUD behavior from the generic repository.</summary>
public sealed class StockRepository(NEXTDbContext context) : TRepository<Stock, Guid>(context), IStockRepository
{
    public Task<Stock?> GetForProductAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default)
        => context.Stocks.AsNoTracking().Include(x => x.Product).Include(x => x.Warehouse).FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId, cancellationToken);
    public Task<List<Stock>> GetByWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => context.Stocks.AsNoTracking()
        .Include(x => x.Product)
        .Where(x => x.WarehouseId == warehouseId)
        .OrderBy(x => x.Product.Name)
        .ToListAsync(cancellationToken);
}
