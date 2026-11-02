using MediatR;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.RolePermissions;

public sealed record RevokePermissionFromRoleCommand(Guid RoleId, Guid PermissionId, string? Reason = null) : IRequest;
