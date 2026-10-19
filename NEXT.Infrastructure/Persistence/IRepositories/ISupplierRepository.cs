using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Supplier. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ISupplierRepository : IRepository<Supplier, Guid>
{
    Task<Supplier?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<Supplier>> SearchAsync(string? term, bool? isActive, int skip, int take, CancellationToken cancellationToken = default);
}
