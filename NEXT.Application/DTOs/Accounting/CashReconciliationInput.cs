using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;
public sealed class CashReconciliationInput
{
    public Guid OperationId { get; set; }
    public AccountType Account { get; set; }
    public DateOnly Date { get; set; }
    public decimal ExternalBalance { get; set; }
    public decimal DepositsInTransit { get; set; }
    public decimal OutstandingPayments { get; set; }
    [Required] public string PositionHash { get; set; } = "";
    [Required, StringLength(200)] public string Evidence { get; set; } = "";
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}
