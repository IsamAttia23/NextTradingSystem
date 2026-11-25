using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Invoices;

public sealed class CancelInvoiceInput
{
    public Guid OperationId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    [Required, StringLength(500)]
    public string Reason { get; set; } = "";
    public Guid? CustomerId { get; set; }
}
