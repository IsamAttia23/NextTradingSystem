using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for StockMovement. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IStockMovementRepository : IKeyRepository<StockMovement, Guid>
{
    Expression<Func<StockMovement, bool>> BuildFilterExpression(StockMovementQueryModel query);
}
