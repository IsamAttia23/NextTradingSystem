using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IProductRepository : IKeyRepository<Product, Guid>
{
    Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
    Expression<Func<Product, bool>> BuildFilterExpression(ProductQueryModel query);
    Task<List<Product>> GetLowStockAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default);
}
