using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;
public sealed class CashTransferInput
{
    public Guid OperationId { get; set; }
    public AccountType FromAccount { get; set; }
    public AccountType ToAccount { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}
