using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for PurchaseOrder. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IPurchaseOrderRepository : IRepository<PurchaseOrder, Guid>
{
    Task<PurchaseOrder?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<PurchaseOrder>> SearchAsync(DateOnly? from, DateOnly? to, Guid? supplierId, PurchaseOrderStatus? status, int skip, int take, CancellationToken cancellationToken = default);
}
