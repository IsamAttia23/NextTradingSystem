using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Supplier; inherits common CRUD behavior from the generic repository.</summary>
public sealed class SupplierRepository(NEXTDbContext context) : TRepository<Supplier, Guid>(context), ISupplierRepository
{
    public Task<Supplier?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => context.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
    public Task<List<Supplier>> SearchAsync(string? term, bool? isActive, int skip, int take, CancellationToken cancellationToken = default)
    {
        var q = context.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var t = term.Trim();
            q = q.Where(x => x.Name.Contains(t) || (x.Code != null && x.Code.Contains(t))
            || (x.Phone != null && x.Phone.Contains(t)) || (x.Email != null && x.Email.Contains(t)));
        }
        if (isActive.HasValue) q = q.Where(x => x.IsActive == isActive);
        return q.OrderBy(x => x.Name).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
    }
}
