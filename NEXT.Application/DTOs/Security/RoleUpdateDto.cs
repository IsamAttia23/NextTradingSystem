using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Security;

public sealed record RoleUpdateDto
{
    [Required, StringLength(50)]
    public string Name { get; init; } = "";

    [StringLength(500)]
    public string? Description { get; init; }
}