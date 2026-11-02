using MediatR;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.RolePermissions;

public sealed record GetRolePermissionsQuery : IRequest<IReadOnlyList<RolePermissionDto>>;
