using MediatR;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.Roles;

public sealed record CreateRoleCommand(RoleCreationDto Data) : IRequest<RoleDto>;
public sealed record UpdateRoleCommand(Guid Id, RoleUpdateDto Data) : IRequest<RoleDto>;
public sealed record DeleteRoleCommand(Guid Id) : IRequest;
public sealed record GetRoleByIdQuery(Guid Id) : IRequest<RoleDto?>;
public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;
