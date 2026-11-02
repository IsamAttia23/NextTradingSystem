using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IPurchaseOrderItemRepository : IKeyRepository<PurchaseOrderItem, Guid>
{

}
