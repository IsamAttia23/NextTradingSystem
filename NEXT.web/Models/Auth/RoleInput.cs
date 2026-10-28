using System.ComponentModel.DataAnnotations;

namespace NEXT.web.Models.Auth;

public sealed class RoleInput
{
    [Required, StringLength(50)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
}
