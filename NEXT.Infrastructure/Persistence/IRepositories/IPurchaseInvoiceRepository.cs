using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for PurchaseInvoice. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IPurchaseInvoiceRepository : IRepository<PurchaseInvoice, Guid>
{
    Task<PurchaseInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<PurchaseInvoice>> SearchAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? supplierId, InvoiceStatus? status, int skip, int take, CancellationToken cancellationToken = default);
}
