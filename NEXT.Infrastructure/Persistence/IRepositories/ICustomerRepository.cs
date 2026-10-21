using NEXT.Domain.Entities;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for Customer. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ICustomerRepository : IRepository<Customer, Guid>
{
    IQueryable<Customer> Search(CustomerQueryModel customerQuery);
}
