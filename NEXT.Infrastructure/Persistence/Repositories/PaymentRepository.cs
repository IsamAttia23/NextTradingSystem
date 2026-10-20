using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for Payment; inherits common CRUD behavior from the generic repository.</summary>
public sealed class PaymentRepository(NEXTDbContext context) : TRepository<Payment, Guid>(context), IPaymentRepository
{
    public Task<List<Payment>> SearchAsync(DateOnly? from, DateOnly? to, Guid? customerId, Guid? supplierId, CancellationToken cancellationToken = default)
    {
        var q = context.Payments.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Supplier)
            .AsQueryable();
        if (from.HasValue) q = q.Where(x => x.PaymentDate >= from);
        if (to.HasValue) q = q.Where(x => x.PaymentDate <= to);
        if (customerId.HasValue) q = q.Where(x => x.CustomerId == customerId);
        if (supplierId.HasValue) q = q.Where(x => x.SupplierId == supplierId);
        return q.OrderByDescending(x => x.PaymentDate).ToListAsync(cancellationToken);
    }
}
