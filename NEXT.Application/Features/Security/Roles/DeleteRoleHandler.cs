using MapsterMapper;
using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Security.Roles;

public sealed class DeleteRoleHandler(IRoleManagementService roles, NEXT.Application.Abstractions.Context.IDeletionAuditContext deletionAudit) : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roles.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.Id}' was not found.");
        if (role.IsSystem)
        {
            throw new UnprocessableEntityException("System roles cannot be deleted.");
        }
        using var audit = deletionAudit.Begin(NEXT.Application.Services.DeletionReason.Require(request.Reason));
        var result = await roles.DeleteAsync(role, cancellationToken);
        if (!result.Succeeded)
        {
            throw new UnprocessableEntityException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }
}
