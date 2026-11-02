using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.Security.RolePermissions;

public sealed class RevokePermissionFromRoleHandler(IRolePermissionService service, NEXT.Application.Abstractions.Context.IDeletionAuditContext deletionAudit) : IRequestHandler<RevokePermissionFromRoleCommand>
{
    public async Task Handle(RevokePermissionFromRoleCommand request, CancellationToken cancellationToken)
    {
        using var audit = deletionAudit.Begin(NEXT.Application.Services.DeletionReason.Require(request.Reason));
        await service.RemoveAsync(request.RoleId, request.PermissionId, cancellationToken);
    }
}
