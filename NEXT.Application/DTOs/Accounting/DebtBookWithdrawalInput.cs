using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed class DebtBookWithdrawalInput : NEXT.Application.DTOs.Invoices.InvoiceDraftInput
{
    [StringLength(50)] public string? CustomerPhone { get; set; }
    [Required] public byte[] RowVersion { get; set; } = [];
}
