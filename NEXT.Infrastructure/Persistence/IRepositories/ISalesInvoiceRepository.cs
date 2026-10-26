using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for SalesInvoice. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ISalesInvoiceRepository : IKeyRepository<SalesInvoice, Guid>
{
    Task<SalesInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    IQueryable<SalesInvoice> Search(SalesInvoiceQueryModel queryModel);
}
