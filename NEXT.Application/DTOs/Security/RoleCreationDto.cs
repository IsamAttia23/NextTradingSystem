using System.ComponentModel.DataAnnotations;
namespace NEXT.Application.DTOs.Security;
public sealed record RoleCreationDto
{
    public RoleCreationDto() { }
    public RoleCreationDto(string name, string? description, bool isSystem = false) { Name = name; Description = description; IsSystem = isSystem; }
    [Required, StringLength(50)] public string Name { get; init; } = "";
    [StringLength(500)] public string? Description { get; init; }
    public bool IsSystem { get; init; }
}
