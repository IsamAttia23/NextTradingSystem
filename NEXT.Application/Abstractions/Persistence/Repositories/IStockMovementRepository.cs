using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IStockMovementRepository : IKeyRepository<StockMovement, Guid>
{
    Expression<Func<StockMovement, bool>> BuildFilterExpression(StockMovementQueryModel query);
}
