using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Stock. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IStockRepository : IKeyRepository<Stock, Guid>
{
    Task<Stock?> GetForProductAsync(Guid productId, Guid warehouseId, CancellationToken cancellationToken = default);
    IQueryable<Stock> GetByWarehouseAsync(Guid warehouseId);
}
