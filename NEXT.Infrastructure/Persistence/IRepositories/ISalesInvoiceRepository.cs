using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for SalesInvoice. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ISalesInvoiceRepository : IRepository<SalesInvoice, Guid>
{
    Task<SalesInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<SalesInvoice>> SearchAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? customerId, InvoiceStatus? status, int skip, int take, CancellationToken cancellationToken = default);
}
