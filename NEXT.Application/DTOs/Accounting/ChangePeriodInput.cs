using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Accounting;
public sealed class ChangePeriodInput
{
    public Guid Id { get; set; }
    public long Version { get; set; }
    public bool Close { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}
