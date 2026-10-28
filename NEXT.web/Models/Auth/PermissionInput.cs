using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models.Auth;

public sealed class PermissionInput
{
    [Required, StringLength(120)] public string Code { get; set; } = "";
    [Required, StringLength(80)] public string Module { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
}
