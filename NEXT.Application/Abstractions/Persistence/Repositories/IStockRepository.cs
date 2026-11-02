using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IStockRepository : IKeyRepository<Stock, Guid>
{
    Task<Stock?> GetForProductAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default);
    IQueryable<Stock> GetByWarehouseAsync(Guid warehouseId);
}
