using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Security;

public sealed record RolePermissionCreationDto(
    [property: Required, NEXT.Application.Validation.NotEmptyGuid] Guid RoleId,
    [property: Required, NEXT.Application.Validation.NotEmptyGuid] Guid PermissionId);
