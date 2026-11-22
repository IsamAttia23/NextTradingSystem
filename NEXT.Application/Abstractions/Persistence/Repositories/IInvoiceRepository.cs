using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface IInvoiceRepository : IKeyRepository<Invoice, Guid>
{
    Task<Invoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Expression<Func<Invoice, bool>> BuildFilterExpression(InvoiceQueryModel query);
}
