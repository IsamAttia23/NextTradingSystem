
using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Application.QueryModels;

public sealed class PurchaseInvoiceQueryModel : PaginationQuery
{
    // Invoice filters
    public string? InvoiceNumber { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? PurchaseOrderId { get; set; }

    // Invoice date range
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }

    // Amount ranges
    public decimal? MinSubTotal { get; set; }
    public decimal? MaxSubTotal { get; set; }

    public decimal? MinDiscountAmount { get; set; }
    public decimal? MaxDiscountAmount { get; set; }

    public decimal? MinTaxAmount { get; set; }
    public decimal? MaxTaxAmount { get; set; }

    public decimal? MinTotalAmount { get; set; }
    public decimal? MaxTotalAmount { get; set; }

    public decimal? MinPaidAmount { get; set; }
    public decimal? MaxPaidAmount { get; set; }

    public decimal? MinRemainingAmount { get; set; }
    public decimal? MaxRemainingAmount { get; set; }

    // Status filters
    public PaymentStatus? PaymentStatus { get; set; }
    public InvoiceStatus? Status { get; set; }
}
