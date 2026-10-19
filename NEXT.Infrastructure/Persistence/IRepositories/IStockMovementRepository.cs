using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for StockMovement. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IStockMovementRepository : IRepository<StockMovement, Guid>
{
    Task<List<StockMovement>> GetForProductAsync(Guid productId, Guid? warehouseId, DateTimeOffset? from, DateTimeOffset? to, int skip, int take, CancellationToken cancellationToken = default);
}
