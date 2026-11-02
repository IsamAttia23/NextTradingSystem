using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.Roles;

public sealed class GetRolesHandler(IRoleManagementService roles) : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken) =>
        roles.GetAllAsync<RoleDto>(cancellationToken);
}
