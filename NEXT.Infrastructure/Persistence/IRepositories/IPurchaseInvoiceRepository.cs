using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for PurchaseInvoice. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface IPurchaseInvoiceRepository : IRepository<PurchaseInvoice, Guid>
{
    Task<PurchaseInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    IQueryable<PurchaseInvoice> Search(PurchaseInvoiceQueryModel queryModel);
}
