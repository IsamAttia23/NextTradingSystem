using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IPurchaseInvoiceRepository : IKeyRepository<PurchaseInvoice, Guid>
{
    Task<PurchaseInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Expression<Func<PurchaseInvoice, bool>> BuildFilterExpression(PurchaseInvoiceQueryModel query);
}
