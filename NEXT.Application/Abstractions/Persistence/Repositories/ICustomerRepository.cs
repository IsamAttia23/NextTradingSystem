using NEXT.Domain.Entities;
using NEXT.Application.QueryModels;
using NEXT.Application.Abstractions.Persistence;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Customer. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ICustomerRepository : IKeyRepository<Customer, Guid>
{
    IQueryable<Customer> Search(CustomerQueryModel customerQuery);
}
