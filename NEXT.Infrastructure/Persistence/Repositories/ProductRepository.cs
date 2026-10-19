using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Product; inherits common CRUD behavior from the generic repository.</summary>
public sealed class ProductRepository(NEXTDbContext context) : Repository<Product, Guid>(context), IProductRepository
{
    public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => context.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Barcode == barcode, cancellationToken);
    public Task<List<Product>> SearchAsync(string? term, Guid? categoryId, bool? isActive, int skip, int take, CancellationToken cancellationToken = default)
    {
        var q = context.Products.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.UnitOfMeasure)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim(); q = q.Where(x => x.Name.Contains(t)
            || (x.Barcode != null && x.Barcode.Contains(t)));
        }
        if (categoryId.HasValue) q = q.Where(x => x.CategoryId == categoryId);
        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive);
        return q.OrderBy(x => x.Name).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
    }
    public Task<List<Product>> GetLowStockAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var q = context.Products.AsNoTracking()
            .Include(x => x.Stocks)
            .Where(x => x.IsActive && x.Stocks
            .Any(s => s.Quantity - s.ReservedQuantity <= x.MinStock));

        if (warehouseId.HasValue) q = q.Where(x => x.Stocks
        .Any(s => s.WarehouseId == warehouseId && s.Quantity - s.ReservedQuantity <= x.MinStock));
        return q.OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }
}
