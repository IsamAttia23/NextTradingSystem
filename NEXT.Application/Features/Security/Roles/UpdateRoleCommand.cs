using MediatR;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.Roles;

public sealed record UpdateRoleCommand(Guid Id, RoleUpdateDto Data) : IRequest<RoleDto>;
