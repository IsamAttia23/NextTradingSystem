using NEXT.Application.Common.QueryModels;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Application.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for SalesInvoice; inherits common CRUD behavior from the generic repository.</summary>
public sealed class SalesInvoiceRepository(NEXTDbContext context) : KeyRepository<SalesInvoice, Guid>(context), ISalesInvoiceRepository
{
    public override IQueryable<SalesInvoice> Search(PaginationQuery query) =>
        Search(query as SalesInvoiceQueryModel ?? new SalesInvoiceQueryModel { SearchTerm = query.SearchTerm, SortBy = query.SortBy,
            SortDirection = query.SortDirection, PageNumber = query.PageNumber, PageSize = query.PageSize });

    public Task<SalesInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => Context.SalesInvoices.AsNoTracking()
        .Include(x => x.Customer)
        .Include(x => x.Warehouse)
        .Include(x => x.User)
        .Include(x => x.Items)
        .ThenInclude(i => i.Product)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public IQueryable<SalesInvoice> Search(SalesInvoiceQueryModel queryModel)
    {
        ArgumentNullException.ThrowIfNull(queryModel);

        var invoices = Context.SalesInvoices
            .AsNoTracking()
            .AsQueryable();

        invoices = ApplyDateFilters(invoices, queryModel);
        invoices = ApplyCustomerFilter(invoices, queryModel);
        invoices = ApplyStatusFilter(invoices, queryModel);

        return ApplySorting(
            invoices,
            queryModel.SortBy,
            queryModel.SortDirection);
    }

    private static IQueryable<SalesInvoice> ApplyDateFilters(IQueryable<SalesInvoice> invoices, SalesInvoiceQueryModel queryModel)
    {
        if (queryModel.From.HasValue)
        {
            invoices = invoices.Where(
                x => x.InvoiceDate >= queryModel.From.Value);
        }

        if (queryModel.To.HasValue)
        {
            invoices = invoices.Where(
                x => x.InvoiceDate <= queryModel.To.Value);
        }

        return invoices;
    }

    private static IQueryable<SalesInvoice> ApplyCustomerFilter( IQueryable<SalesInvoice> invoices, SalesInvoiceQueryModel queryModel)
    {
        if (queryModel.CustomerId.HasValue)
        {
            invoices = invoices.Where(
                x => x.CustomerId == queryModel.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryModel.CustomerName))
        {
            var customerName = queryModel.CustomerName.Trim();

            invoices = invoices.Where(
                x => x.Customer != null && x.Customer.Name.Contains(customerName));
        }

        return invoices;
    }

    private static IQueryable<SalesInvoice> ApplyStatusFilter(IQueryable<SalesInvoice> invoices,SalesInvoiceQueryModel queryModel)
    {
        if (queryModel.Status.HasValue)
        {
            invoices = invoices.Where(
                x => x.Status == queryModel.Status.Value);
        }

        return invoices;
    }

    private static IQueryable<SalesInvoice> ApplySorting(IQueryable<SalesInvoice> invoices,string? sortBy, SortDirection? direction)
    {
        var descending = direction == SortDirection.Descending;

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "invoicedate" => descending
                ? invoices.OrderByDescending(x => x.InvoiceDate)
                    .ThenByDescending(x => x.Id)
                : invoices.OrderBy(x => x.InvoiceDate)
                    .ThenBy(x => x.Id),

            _ => descending
                ? invoices.OrderByDescending(x => x.InvoiceDate)
                    .ThenByDescending(x => x.Id)
                : invoices.OrderBy(x => x.InvoiceDate)
                    .ThenBy(x => x.Id)
        };
    }
}
