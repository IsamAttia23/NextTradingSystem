using NEXT.Domain.Entities;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for StockMovement. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IStockMovementRepository : IRepository<StockMovement, Guid>
{
    IQueryable<StockMovement> GetForProduct(StockMovementQueryModel queryModel, CancellationToken cancellationToken = default);
}
