using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Customer. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ICustomerRepository : IRepository<Customer, Guid>
{
    Task<List<Customer>> SearchAsync(string? term, bool? isActive, int skip, int take, CancellationToken cancellationToken = default);
}
