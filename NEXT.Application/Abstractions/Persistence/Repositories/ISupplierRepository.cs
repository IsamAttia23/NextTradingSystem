using System.Linq.Expressions;
using NEXT.Domain.Entities;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

public interface ISupplierRepository : IKeyRepository<Supplier, Guid>
{
    Task<Supplier?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Expression<Func<Supplier, bool>> BuildFilterExpression(SupplierQueryModel query);
}
