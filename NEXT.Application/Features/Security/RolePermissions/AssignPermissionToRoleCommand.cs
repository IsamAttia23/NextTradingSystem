using MediatR;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.RolePermissions;

public sealed record AssignPermissionToRoleCommand(RolePermissionCreationDto Data) : IRequest;
