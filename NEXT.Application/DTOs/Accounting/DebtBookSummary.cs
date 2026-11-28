using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;

public sealed class DebtBookSummary
{
    public Guid Id { get; set; }
    public int CycleNumber { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public decimal Balance { get; set; }
    public DateTimeOffset LastActivity { get; set; }
}
