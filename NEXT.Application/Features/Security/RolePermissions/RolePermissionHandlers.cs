using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Validation;
using NEXT.Application.Exceptions;

namespace NEXT.Application.Features.Security.RolePermissions;

public sealed class AssignPermissionToRoleHandler(
    IRolePermissionService service,
    IEntityValidator<RolePermissionCreationDto> validator) : IRequestHandler<AssignPermissionToRoleCommand>
{
    public async Task Handle(AssignPermissionToRoleCommand request, CancellationToken cancellationToken)
    {
        validator.Validate(request.Data);
        if (request.Data.RoleId == Guid.Empty || request.Data.PermissionId == Guid.Empty)
            throw new NEXT.Application.Exceptions.AppValidationException(new Dictionary<string, string[]> {
                ["RolePermission"] = ["Both RoleId and PermissionId are required."]
            });
        if (!await service.RoleExistsAsync(request.Data.RoleId, cancellationToken))
            throw new NotFoundException($"Role '{request.Data.RoleId}' was not found.");
        if (!await service.PermissionExistsAsync(request.Data.PermissionId, cancellationToken))
            throw new NotFoundException($"Permission '{request.Data.PermissionId}' was not found.");
        if (await service.AssignmentExistsAsync(request.Data.RoleId, request.Data.PermissionId, cancellationToken))
            throw new AppValidationException(new Dictionary<string, string[]> {
                ["PermissionId"] = ["This permission is already assigned to the role."]
            });
        await service.AddAsync(request.Data.RoleId, request.Data.PermissionId, cancellationToken);
    }
}

public sealed class RevokePermissionFromRoleHandler(IRolePermissionService service) : IRequestHandler<RevokePermissionFromRoleCommand>
{
    public async Task Handle(RevokePermissionFromRoleCommand request, CancellationToken cancellationToken)
    {
        if (!await service.AssignmentExistsAsync(request.RoleId, request.PermissionId, cancellationToken))
            throw new NotFoundException("The role-permission assignment was not found.");
        await service.RemoveAsync(request.RoleId, request.PermissionId, cancellationToken);
    }
}

public sealed class GetRolePermissionsHandler(IRolePermissionService service) : IRequestHandler<GetRolePermissionsQuery, IReadOnlyList<RolePermissionDto>>
{
    public Task<IReadOnlyList<RolePermissionDto>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken) => service.GetAllAsync(cancellationToken);
}

public sealed class GetPermissionsForRoleHandler(IRolePermissionService service) : IRequestHandler<GetPermissionsForRoleQuery, IReadOnlyList<RolePermissionDto>>
{
    public async Task<IReadOnlyList<RolePermissionDto>> Handle(GetPermissionsForRoleQuery request, CancellationToken cancellationToken)
    {
        if (!await service.RoleExistsAsync(request.RoleId, cancellationToken))
            throw new NotFoundException($"Role '{request.RoleId}' was not found.");
        return await service.GetByRoleAsync(request.RoleId, cancellationToken);
    }
}
