using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Security;

public sealed class PermissionCreationDto
{
    [Required, StringLength(120)]
    public string Code { get; set; } = string.Empty;
    [Required, StringLength(80)]
    public string Module { get; set; } = string.Empty;
    [StringLength(500)]
    public string? Description { get; set; }
}