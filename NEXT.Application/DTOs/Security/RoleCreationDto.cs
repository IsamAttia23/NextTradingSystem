using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Security;

public sealed record RoleCreationDto(
    [property: Required, StringLength(50)] string Name,
    [property: StringLength(500)] string? Description,
    bool IsSystem = false);