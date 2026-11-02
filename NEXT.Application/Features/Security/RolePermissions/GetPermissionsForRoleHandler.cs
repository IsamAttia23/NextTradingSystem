using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.Security.RolePermissions;

public sealed class GetPermissionsForRoleHandler(IRolePermissionService service) : IRequestHandler<GetPermissionsForRoleQuery, IReadOnlyList<RolePermissionDto>>
{
    public Task<IReadOnlyList<RolePermissionDto>> Handle(GetPermissionsForRoleQuery request, CancellationToken cancellationToken) => service.GetByRoleAsync(request.RoleId, cancellationToken);
}
