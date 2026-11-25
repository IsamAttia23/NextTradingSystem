using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed class ApproveOpeningInput
{
    public Guid OperationId { get; set; }
    public AccountType Account { get; set; }
    public DateOnly Date { get; set; }
    public decimal VerifiedBalance { get; set; }
    public string PositionHash { get; set; } = "";
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}
