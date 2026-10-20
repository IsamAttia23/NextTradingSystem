using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for CustomerContact; inherits common CRUD behavior from the generic repository.</summary>
public sealed class CustomerContactRepository(NEXTDbContext context) : TRepository<CustomerContact, Guid>(context), ICustomerContactRepository
{

}
