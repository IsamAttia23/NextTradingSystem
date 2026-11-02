using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface ISalesInvoiceRepository : IKeyRepository<SalesInvoice, Guid>
{
    Task<SalesInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Expression<Func<SalesInvoice, bool>> BuildFilterExpression(SalesInvoiceQueryModel query);
}
