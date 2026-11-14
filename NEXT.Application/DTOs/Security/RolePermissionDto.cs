namespace NEXT.Application.DTOs.Security;

public sealed record RolePermissionDto
{
    public Guid RoleId { get; init; }
    public Guid PermissionId { get; init; }
    public string? RoleName { get; init; }
    public string Code { get; init; } = default!;
    public string Module { get; init; } = default!;
}
