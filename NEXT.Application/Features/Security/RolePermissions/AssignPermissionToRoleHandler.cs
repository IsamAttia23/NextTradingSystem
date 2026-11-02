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
