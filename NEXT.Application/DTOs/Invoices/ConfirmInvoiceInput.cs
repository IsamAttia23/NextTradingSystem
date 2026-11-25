using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;

namespace NEXT.Application.DTOs.Invoices;

public sealed class ConfirmInvoiceInput
{
    public Guid OperationId { get; set; }
    public Guid? CustomerId { get; set; }
    [Required]
    public byte[] RowVersion { get; set; } = [];
    public decimal Amount { get; set; }
    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod? PaymentMethod { get; set; }
}
