namespace NEXT.Application.DTOs.Security;

public sealed record RoleDto(Guid Id, string? Name, string? NormalizedName, string? Description, bool IsSystem);