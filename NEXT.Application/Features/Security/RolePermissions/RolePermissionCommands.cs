using MediatR;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.RolePermissions;

public sealed record AssignPermissionToRoleCommand(RolePermissionCreationDto Data) : IRequest;
public sealed record RevokePermissionFromRoleCommand(Guid RoleId, Guid PermissionId, string? Reason = null) : IRequest;
public sealed record GetRolePermissionsQuery : IRequest<IReadOnlyList<RolePermissionDto>>;
public sealed record GetPermissionsForRoleQuery(Guid RoleId) : IRequest<IReadOnlyList<RolePermissionDto>>;
