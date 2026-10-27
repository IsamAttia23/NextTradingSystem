namespace NEXT.Application.DTOs.Security;

public sealed record RolePermissionDto(Guid RoleId, Guid PermissionId, string? RoleName, string Code, string Module);