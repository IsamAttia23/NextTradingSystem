using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Domain.QueryModels;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

/// <summary>EF Core repository for PurchaseInvoice; inherits common CRUD behavior from the generic repository.</summary>
public sealed class PurchaseInvoiceRepository(NEXTDbContext context) : KeyRepository<PurchaseInvoice, Guid>(context), IPurchaseInvoiceRepository
{
    public Task<PurchaseInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => context.PurchaseInvoices.AsNoTracking().Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(i => i.Product).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public IQueryable<PurchaseInvoice> Search(PurchaseInvoiceQueryModel queryModel)
    {
        ArgumentNullException.ThrowIfNull(queryModel);

        var invoices = context.PurchaseInvoices
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Warehouse)
            .Include(x => x.PurchaseOrder)
            .AsQueryable();

        invoices = ApplySearchTerm(invoices, queryModel);
        invoices = ApplyRelationshipFilters(invoices, queryModel);
        invoices = ApplyDateFilters(invoices, queryModel);
        invoices = ApplyAmountFilters(invoices, queryModel);
        invoices = ApplyStatusFilters(invoices, queryModel);

        return ApplySorting(
            invoices,
            queryModel.SortBy,
            queryModel.SortDirection);
    }

    // Search by invoice number or notes
    private static IQueryable<PurchaseInvoice> ApplySearchTerm(IQueryable<PurchaseInvoice> invoices, PurchaseInvoiceQueryModel queryModel)
    {
        if (string.IsNullOrWhiteSpace(queryModel.SearchTerm))
            return invoices;

        var term = queryModel.SearchTerm.Trim();

        return invoices.Where(x =>
            x.InvoiceNumber.Contains(term) ||
            (x.Notes != null && x.Notes.Contains(term)));
    }

    // Filter by supplier, warehouse and purchase order
    private static IQueryable<PurchaseInvoice> ApplyRelationshipFilters(IQueryable<PurchaseInvoice> invoices, PurchaseInvoiceQueryModel queryModel)
    {
        if (queryModel.SupplierId.HasValue)
        {
            invoices = invoices.Where(x =>
                x.SupplierId == queryModel.SupplierId.Value);
        }

        if (queryModel.WarehouseId.HasValue)
        {
            invoices = invoices.Where(x =>
                x.WarehouseId == queryModel.WarehouseId.Value);
        }

        if (queryModel.PurchaseOrderId.HasValue)
        {
            invoices = invoices.Where(x =>
                x.PurchaseOrderId == queryModel.PurchaseOrderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryModel.InvoiceNumber))
        {
            var invoiceNumber = queryModel.InvoiceNumber.Trim();

            invoices = invoices.Where(x =>
                x.InvoiceNumber.Contains(invoiceNumber));
        }

        return invoices;
    }

    // Filter by invoice date
    private static IQueryable<PurchaseInvoice> ApplyDateFilters(IQueryable<PurchaseInvoice> invoices, PurchaseInvoiceQueryModel queryModel)
    {
        if (queryModel.From.HasValue)
        {
            invoices = invoices.Where(x =>
                x.InvoiceDate >= queryModel.From.Value);
        }

        if (queryModel.To.HasValue)
        {
            invoices = invoices.Where(x =>
                x.InvoiceDate <= queryModel.To.Value);
        }

        return invoices;
    }

    // Filter by invoice amounts
    private static IQueryable<PurchaseInvoice> ApplyAmountFilters(IQueryable<PurchaseInvoice> invoices, PurchaseInvoiceQueryModel queryModel)
    {
        if (queryModel.MinSubTotal.HasValue)
            invoices = invoices.Where(x =>
                x.SubTotal >= queryModel.MinSubTotal.Value);

        if (queryModel.MaxSubTotal.HasValue)
            invoices = invoices.Where(x =>
                x.SubTotal <= queryModel.MaxSubTotal.Value);

        if (queryModel.MinDiscountAmount.HasValue)
            invoices = invoices.Where(x =>
                x.DiscountAmount >= queryModel.MinDiscountAmount.Value);

        if (queryModel.MaxDiscountAmount.HasValue)
            invoices = invoices.Where(x =>
                x.DiscountAmount <= queryModel.MaxDiscountAmount.Value);

        if (queryModel.MinTaxAmount.HasValue)
            invoices = invoices.Where(x =>
                x.TaxAmount >= queryModel.MinTaxAmount.Value);

        if (queryModel.MaxTaxAmount.HasValue)
            invoices = invoices.Where(x =>
                x.TaxAmount <= queryModel.MaxTaxAmount.Value);

        if (queryModel.MinTotalAmount.HasValue)
            invoices = invoices.Where(x =>
                x.TotalAmount >= queryModel.MinTotalAmount.Value);

        if (queryModel.MaxTotalAmount.HasValue)
            invoices = invoices.Where(x =>
                x.TotalAmount <= queryModel.MaxTotalAmount.Value);

        if (queryModel.MinPaidAmount.HasValue)
            invoices = invoices.Where(x =>
                x.PaidAmount >= queryModel.MinPaidAmount.Value);

        if (queryModel.MaxPaidAmount.HasValue)
            invoices = invoices.Where(x =>
                x.PaidAmount <= queryModel.MaxPaidAmount.Value);

        if (queryModel.MinRemainingAmount.HasValue)
            invoices = invoices.Where(x =>
                x.RemainingAmount >= queryModel.MinRemainingAmount.Value);

        if (queryModel.MaxRemainingAmount.HasValue)
            invoices = invoices.Where(x =>
                x.RemainingAmount <= queryModel.MaxRemainingAmount.Value);

        return invoices;
    }

    // Filter by payment and invoice status
    private static IQueryable<PurchaseInvoice> ApplyStatusFilters(IQueryable<PurchaseInvoice> invoices, PurchaseInvoiceQueryModel queryModel)
    {
        if (queryModel.PaymentStatus.HasValue)
        {
            invoices = invoices.Where(x =>
                x.PaymentStatus == queryModel.PaymentStatus.Value);
        }

        if (queryModel.Status.HasValue)
        {
            invoices = invoices.Where(x =>
                x.Status == queryModel.Status.Value);
        }

        return invoices;
    }

    // Sorting
    private static IQueryable<PurchaseInvoice> ApplySorting(IQueryable<PurchaseInvoice> invoices, string? sortBy, SortDirection? direction)
    {
        var descending = direction == SortDirection.Descending;

        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "invoicenumber" => descending
                ? invoices.OrderByDescending(x => x.InvoiceNumber)
                    .ThenByDescending(x => x.InvoiceDate)
                : invoices.OrderBy(x => x.InvoiceNumber)
                    .ThenBy(x => x.InvoiceDate),

            "invoicedate" => descending
                ? invoices.OrderByDescending(x => x.InvoiceDate)
                    .ThenByDescending(x => x.InvoiceNumber)
                : invoices.OrderBy(x => x.InvoiceDate)
                    .ThenBy(x => x.InvoiceNumber),

            "subtotal" => descending
                ? invoices.OrderByDescending(x => x.SubTotal)
                    .ThenByDescending(x => x.InvoiceDate)
                : invoices.OrderBy(x => x.SubTotal)
                    .ThenBy(x => x.InvoiceDate),

            "discountamount" => descending
                ? invoices.OrderByDescending(x => x.DiscountAmount)
                    .ThenByDescending(x => x.InvoiceDate)
                : invoices.OrderBy(x => x.DiscountAmount)
                    .ThenBy(x => x.InvoiceDate),

            "taxamount" => descending
                ? invoices.OrderByDescending(x => x.TaxAmount)
                    .ThenByDescending(x => x.InvoiceDate)
                : invoices.OrderBy(x => x.TaxAmount)
                    .ThenBy(x => x.InvoiceDate),

            "totalamount" => descending
                ? invoices.OrderByDescending(x => x.TotalAmount)
                    .ThenByDescending(x => x.InvoiceDate)
                : invoices.OrderBy(x => x.TotalAmount)
                    .ThenBy(x => x.InvoiceDate),

            "paidamount" => descending
                ? invoices.OrderByDescending(x => x.PaidAmount)
                    .ThenByDescending(x => x.InvoiceDate)
                : invoices.OrderBy(x => x.PaidAmount)
                    .ThenBy(x => x.InvoiceDate),

            "remainingamount" => descending
                ? invoices.OrderByDescending(x => x.RemainingAmount)
                    .ThenByDescending(x => x.InvoiceDate)
                : invoices.OrderBy(x => x.RemainingAmount)
                    .ThenBy(x => x.InvoiceDate),

            _ => descending
                ? invoices.OrderByDescending(x => x.InvoiceDate)
                    .ThenByDescending(x => x.InvoiceNumber)
                : invoices.OrderBy(x => x.InvoiceDate)
                    .ThenBy(x => x.InvoiceNumber)
        };
    }
}