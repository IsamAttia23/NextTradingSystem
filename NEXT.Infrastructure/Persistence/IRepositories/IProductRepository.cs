using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Product. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IProductRepository : IRepository<Product, Guid>
{
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Task<List<Product>> SearchAsync(string? term, Guid? categoryId, bool? isActive, int skip, int take, CancellationToken cancellationToken = default);
    Task<List<Product>> GetLowStockAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default);
}
