using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Stock. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IStockRepository : IRepository<Stock, Guid>
{
    Task<Stock?> GetForProductAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default);
    Task<List<Stock>> GetByWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default);
}
