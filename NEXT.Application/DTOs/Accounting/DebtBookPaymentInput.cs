using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed class DebtBookPaymentInput
{
    public Guid OperationId { get; set; }
    [Required] public byte[] RowVersion { get; set; } = [];
    public decimal Amount { get; set; }
    [Required, EnumDataType(typeof(PaymentMethod))] public PaymentMethod? PaymentMethod { get; set; }
    [Required, EnumDataType(typeof(AccountType))] public AccountType? CashAccount { get; set; }
    [Required(ErrorMessage = "DebtBookPaymentDescriptionRequired"), StringLength(500)]
    public string? Notes { get; set; }
}
