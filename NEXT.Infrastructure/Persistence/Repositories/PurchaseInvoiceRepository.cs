using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for PurchaseInvoice; inherits common CRUD behavior from the generic repository.</summary>
public sealed class PurchaseInvoiceRepository(NEXTDbContext context) : Repository<PurchaseInvoice, Guid>(context), IPurchaseInvoiceRepository
{
    public Task<PurchaseInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => context.PurchaseInvoices.AsNoTracking().Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(i => i.Product).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<List<PurchaseInvoice>> SearchAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? supplierId, InvoiceStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var q = context.PurchaseInvoices.AsNoTracking()
            .Include(x => x.Supplier)
            .AsQueryable();
        if (from.HasValue) q = q.Where(x => x.InvoiceDate >= from);
        if (to.HasValue) q = q.Where(x => x.InvoiceDate < to);
        if (supplierId.HasValue) q = q.Where(x => x.SupplierId == supplierId);
        if (status.HasValue) q = q.Where(x => x.Status == status);
        return q.OrderByDescending(x => x.InvoiceDate).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
    }
}
