using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence.IRepositories;

namespace NEXT.Application.Abstractions.Persistence.Repositories;

/// <summary>Persistence contract for CustomerContact. Domain-specific queries live here; EF Core details remain in Infrastructure.</summary>
public interface ICustomerContactRepository : IRepository<CustomerContact, Guid>
{

}
