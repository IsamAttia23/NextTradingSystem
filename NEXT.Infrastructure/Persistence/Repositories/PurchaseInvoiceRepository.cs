using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Persistence.Repositories;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.QueryModels;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Infrastructure.Persistence.Repositories.Entities;

public sealed class PurchaseInvoiceRepository(NEXTDbContext context) : KeyRepository<PurchaseInvoice, Guid>(context), IPurchaseInvoiceRepository
{
    public Task<PurchaseInvoice?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
        => Context.PurchaseInvoices.AsNoTracking().Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(i => i.Product).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public override Expression<Func<PurchaseInvoice, bool>> BuildFilterExpression(PaginationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return BuildFilterExpression(query as PurchaseInvoiceQueryModel ?? new PurchaseInvoiceQueryModel { SearchTerm = query.SearchTerm });
    }

    public Expression<Func<PurchaseInvoice, bool>> BuildFilterExpression(PurchaseInvoiceQueryModel query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var invoiceNumber = query.InvoiceNumber?.Trim();
        Expression<Func<PurchaseInvoice, bool>> filters = entity =>
            (string.IsNullOrEmpty(invoiceNumber) || entity.InvoiceNumber.Contains(invoiceNumber)) &&
            (!query.SupplierId.HasValue || entity.SupplierId == query.SupplierId.Value) &&
            (!query.WarehouseId.HasValue || entity.WarehouseId == query.WarehouseId.Value) &&
            (!query.PurchaseOrderId.HasValue || entity.PurchaseOrderId == query.PurchaseOrderId.Value) &&
            (!query.PaymentStatus.HasValue || entity.PaymentStatus == query.PaymentStatus.Value) &&
            (!query.Status.HasValue || entity.Status == query.Status.Value) &&
            (!query.From.HasValue || entity.InvoiceDate >= query.From.Value) &&
            (!query.To.HasValue || entity.InvoiceDate <= query.To.Value) &&
            (!query.MinSubTotal.HasValue || entity.SubTotal >= query.MinSubTotal.Value) &&
            (!query.MaxSubTotal.HasValue || entity.SubTotal <= query.MaxSubTotal.Value) &&
            (!query.MinDiscountAmount.HasValue || entity.DiscountAmount >= query.MinDiscountAmount.Value) &&
            (!query.MaxDiscountAmount.HasValue || entity.DiscountAmount <= query.MaxDiscountAmount.Value) &&
            (!query.MinTaxAmount.HasValue || entity.TaxAmount >= query.MinTaxAmount.Value) &&
            (!query.MaxTaxAmount.HasValue || entity.TaxAmount <= query.MaxTaxAmount.Value) &&
            (!query.MinTotalAmount.HasValue || entity.TotalAmount >= query.MinTotalAmount.Value) &&
            (!query.MaxTotalAmount.HasValue || entity.TotalAmount <= query.MaxTotalAmount.Value) &&
            (!query.MinPaidAmount.HasValue || entity.PaidAmount >= query.MinPaidAmount.Value) &&
            (!query.MaxPaidAmount.HasValue || entity.PaidAmount <= query.MaxPaidAmount.Value) &&
            (!query.MinRemainingAmount.HasValue || entity.RemainingAmount >= query.MinRemainingAmount.Value) &&
            (!query.MaxRemainingAmount.HasValue || entity.RemainingAmount <= query.MaxRemainingAmount.Value);

        return CombineSearchExpression(filters, query.SearchTerm, "InvoiceNumber", "Notes");
    }

    public override IQueryable<PurchaseInvoice> ApplySorting(IQueryable<PurchaseInvoice> invoices, string? sortBy, SortDirection? sortDirection)
    {
        var descending = sortDirection == SortDirection.Descending;

        var ordered = sortBy?.Trim().ToLowerInvariant() switch
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
        return ordered.ThenBy(entity => entity.Id);
    }
}
