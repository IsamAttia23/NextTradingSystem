using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Invoices;

public sealed class InvoiceFulfillmentInput
{
    public Guid OperationId { get; set; }
    [Required]
    public byte[] RowVersion { get; set; } = [];
    [EnumDataType(typeof(InvoiceStatus))]
    public InvoiceStatus TargetStatus { get; set; }
    [NEXT.Application.Validation.NotEmptyGuid]
    public Guid? CustomerId { get; set; }
    [Phone, StringLength(32)]
    public string? CustomerPhone { get; set; }
    [StringLength(500)]
    public string? Note { get; set; }
}
