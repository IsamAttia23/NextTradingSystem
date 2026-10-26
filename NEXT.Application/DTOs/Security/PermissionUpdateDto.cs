using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Common.Entities;

namespace NEXT.Application.DTOs.Security;

public sealed class PermissionUpdateDto : TIdType<Guid>
{
    public Guid Id { get; set; }
    [Required, StringLength(120)]
    public string Code { get; set; } = string.Empty;
    [Required, StringLength(80)]
    public string Module { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Description { get; set; }
}