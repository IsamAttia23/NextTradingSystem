using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Features.Security.Roles;

public sealed class GetRoleByIdHandler(IRoleManagementService roles) : IRequestHandler<GetRoleByIdQuery, RoleDto?>
{
    public Task<RoleDto?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken) =>
        roles.FindByIdAsync<RoleDto>(request.Id, cancellationToken);
}
