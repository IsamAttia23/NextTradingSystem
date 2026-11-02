using MediatR;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.Roles;

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;
