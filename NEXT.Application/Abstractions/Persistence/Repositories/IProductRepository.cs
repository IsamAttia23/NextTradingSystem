using NEXT.Domain.Entities;
using NEXT.Domain.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Product. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IProductRepository : IKeyRepository<Product, Guid>
{
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    IQueryable<Product> Search(ProductQueryModel queryModel);
    Task<List<Product>> GetLowStockAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default);
}
