using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for PurchaseOrder. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IPurchaseOrderRepository : IKeyRepository<PurchaseOrder, Guid>
{
    Task<PurchaseOrder?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Expression<Func<PurchaseOrder, bool>> BuildFilterExpression(PurchaseOrderQueryModel query);
}
