using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.Security.RolePermissions;

public sealed class AssignPermissionToRoleHandler(
    IRolePermissionService service,
    IEntityValidator<RolePermissionCreationDto> validator) : IRequestHandler<AssignPermissionToRoleCommand>
{
    public Task Handle(AssignPermissionToRoleCommand request, CancellationToken cancellationToken)
    {
        validator.Validate(request.Data);
        if (request.Data.RoleId == Guid.Empty || request.Data.PermissionId == Guid.Empty)
            throw new NEXT.Application.Exceptions.AppValidationException(new Dictionary<string, string[]> {
                ["RolePermission"] = ["Both RoleId and PermissionId are required."]
            });
        return service.AddAsync(request.Data.RoleId, request.Data.PermissionId, cancellationToken);
    }
}

public sealed class RevokePermissionFromRoleHandler(IRolePermissionService service) : IRequestHandler<RevokePermissionFromRoleCommand>
{
    public Task Handle(RevokePermissionFromRoleCommand request, CancellationToken cancellationToken) =>
        service.RemoveAsync(request.RoleId, request.PermissionId, cancellationToken);
}

public sealed class GetRolePermissionsHandler(IRolePermissionService service) : IRequestHandler<GetRolePermissionsQuery, IReadOnlyList<RolePermissionDto>>
{
    public Task<IReadOnlyList<RolePermissionDto>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken) => service.GetAllAsync(cancellationToken);
}

public sealed class GetPermissionsForRoleHandler(IRolePermissionService service) : IRequestHandler<GetPermissionsForRoleQuery, IReadOnlyList<RolePermissionDto>>
{
    public Task<IReadOnlyList<RolePermissionDto>> Handle(GetPermissionsForRoleQuery request, CancellationToken cancellationToken) => service.GetByRoleAsync(request.RoleId, cancellationToken);
}
