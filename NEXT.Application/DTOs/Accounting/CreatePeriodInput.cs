using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;
public sealed class CreatePeriodInput
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}
