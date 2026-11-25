using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Invoices;

public abstract class InvoiceDraftInput
{
    public Guid OperationId { get; set; }
    [Required, StringLength(200)]
    public string CustomerName { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    [EnumDataType(typeof(DiscountType))]
    public DiscountType? DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public List<InvoiceLineInput> Lines { get; set; } = [];
}
