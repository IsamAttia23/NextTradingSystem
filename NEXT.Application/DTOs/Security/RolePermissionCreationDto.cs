using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Security;

public sealed record RolePermissionCreationDto(
    [property: Required] Guid RoleId,
    [property: Required] Guid PermissionId);