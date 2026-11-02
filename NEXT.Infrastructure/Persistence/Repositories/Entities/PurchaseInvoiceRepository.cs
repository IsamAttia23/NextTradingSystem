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
        => StorageErrors.ExecuteAsync(() => Context.PurchaseInvoices.AsNoTracking().Include(x => x.Supplier).Include(x => x.Warehouse).Include(x => x.Items).ThenInclude(i => i.Product).FirstOrDefaultAsync(x => x.Id == id, cancellationToken));

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

    private static readonly string[] SortFields = ["InvoiceNumber", "InvoiceDate", "SubTotal", "DiscountAmount", "TaxAmount", "TotalAmount", "PaidAmount", "RemainingAmount"];
    public override IQueryable<PurchaseInvoice> ApplySorting(IQueryable<PurchaseInvoice> source, string? sortBy, SortDirection? sortDirection)
    {
        var field = SortFields.FirstOrDefault(name => string.Equals(name, sortBy?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "InvoiceDate";
        var descending = sortDirection == SortDirection.Descending;
        var ordered = OrderByField(source, field, descending);
        ordered = ThenByField(ordered, field == "InvoiceDate" ? "InvoiceNumber" : "InvoiceDate", descending);
        return ThenByField(ordered, "Id");
    }
}
