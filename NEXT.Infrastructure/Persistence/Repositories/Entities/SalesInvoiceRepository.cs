using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class SalesInvoiceRepository(NEXTDbContext context) : KeyRepository<SalesInvoice, Guid>(context), ISalesInvoiceRepository
{
    public Task<SalesInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => StorageErrors.ExecuteAsync(() => Context.SalesInvoices.AsNoTracking()
        .Include(x => x.Customer)
        .Include(x => x.Warehouse)
        .Include(x => x.User)
        .Include(x => x.Items)
        .ThenInclude(i => i.Product)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken));

    public override Expression<Func<SalesInvoice, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as SalesInvoiceQueryModel ?? new SalesInvoiceQueryModel { SearchTerm = query.SearchTerm });
    }

    public Expression<Func<SalesInvoice, bool>> BuildFilterExpression(SalesInvoiceQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var customerName = query.CustomerName?.Trim();
        Expression<Func<SalesInvoice, bool>> filters = entity =>
            (!query.From.HasValue || entity.InvoiceDate >= query.From.Value) &&
            (!query.To.HasValue || entity.InvoiceDate <= query.To.Value) &&
            (!query.CustomerId.HasValue || entity.CustomerId == query.CustomerId.Value) &&
            (string.IsNullOrEmpty(customerName) || (entity.Customer != null && entity.Customer.Name.Contains(customerName))) &&
            (!query.Status.HasValue || entity.Status == query.Status.Value);

        return CombineSearchExpression(filters, query.SearchTerm, "InvoiceNumber", "Notes");
    }

    public override IQueryable<SalesInvoice> ApplySorting(IQueryable<SalesInvoice> source, string? sortBy, SortDirection? sortDirection)
    {
        var descending = sortDirection == SortDirection.Descending;
        return ThenByField(OrderByField(source, "InvoiceDate", descending), "Id", descending);
    }
}
