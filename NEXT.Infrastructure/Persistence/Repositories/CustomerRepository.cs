using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Customer; inherits common CRUD behavior from the generic repository.</summary>
public sealed class CustomerRepository(NEXTDbContext context) : Repository<Customer, Guid>(context), ICustomerRepository
{
    public Task<List<Customer>> SearchAsync(string? term, bool? isActive, int skip, int take, CancellationToken cancellationToken = default)
    {
        var q = context.Customers.AsNoTracking().Include(x => x.Contacts).AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            q = q.Where(x => x.Name.Contains(t)
            || (x.Phone != null && x.Phone.Contains(t))
            || (x.Email != null && x.Email.Contains(t)))
                ;
        }

        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive);
        return q.OrderBy(x => x.Name).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
    }
}
