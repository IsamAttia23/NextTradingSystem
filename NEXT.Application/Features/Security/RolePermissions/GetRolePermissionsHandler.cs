using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.Security.RolePermissions;

public sealed class GetRolePermissionsHandler(IRolePermissionService service) : IRequestHandler<GetRolePermissionsQuery, IReadOnlyList<RolePermissionDto>>
{
    public Task<IReadOnlyList<RolePermissionDto>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken) => service.GetAllAsync(cancellationToken);
}
