using NEXT.Domain.Entities;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Supplier. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ISupplierRepository : IRepository<Supplier, Guid>
{
    Task<Supplier?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    IQueryable<Supplier> Search(SupplierQueryModel queryModel);
}
