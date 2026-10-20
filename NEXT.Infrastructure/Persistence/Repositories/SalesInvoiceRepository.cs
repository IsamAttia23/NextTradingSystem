using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for SalesInvoice; inherits common CRUD behavior from the generic repository.</summary>
public sealed class SalesInvoiceRepository(NEXTDbContext context) : TRepository<SalesInvoice, Guid>(context), ISalesInvoiceRepository
{
    public Task<SalesInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default) => context.SalesInvoices.AsNoTracking().Include(x=>x.Customer).Include(x=>x.Warehouse).Include(x=>x.User).Include(x=>x.Items).ThenInclude(i=>i.Product).FirstOrDefaultAsync(x=>x.Id==id,cancellationToken);
    public Task<List<SalesInvoice>> SearchAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? customerId, InvoiceStatus? status, int skip, int take, CancellationToken cancellationToken = default) { var q=context.SalesInvoices.AsNoTracking().Include(x=>x.Customer).AsQueryable(); if(from.HasValue) q=q.Where(x=>x.InvoiceDate>=from); if(to.HasValue) q=q.Where(x=>x.InvoiceDate<to); if(customerId.HasValue) q=q.Where(x=>x.CustomerId==customerId); if(status.HasValue) q=q.Where(x=>x.Status==status); return q.OrderByDescending(x=>x.InvoiceDate).Skip(Math.Max(0,skip)).Take(Math.Clamp(take,1,200)).ToListAsync(cancellationToken); }
}
