using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Security.Roles;

public sealed class CreateRoleHandler(
    IRoleManagementService roles,
    IEntityValidator<RoleCreationDto> validator) : IRequestHandler<CreateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        validator.Validate(request.Data);
        if (await roles.NameExistsAsync(request.Data.Name, cancellationToken: cancellationToken))
            throw new AppValidationException(new Dictionary<string, string[]> { [nameof(request.Data.Name)] = ["This role name already exists."] });

        var role = request.Data.Adapt<Role>();
        role.IsSystem = false;
        var result = await roles.CreateAsync(role);
        if (!result.Succeeded)
            throw new AppValidationException(result.Errors.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.Select(x => x.Description).ToArray()));
        return role.Adapt<RoleDto>();
    }
}

public sealed class UpdateRoleHandler(
    IRoleManagementService roles,
    IEntityValidator<RoleUpdateDto> validator) : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        validator.Validate(request.Data);
        var role = await roles.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.Id}' was not found.");
        if (role.IsSystem && !string.Equals(role.Name, request.Data.Name, StringComparison.Ordinal))
            throw new UnprocessableEntityException("System roles cannot be renamed.");
        if (await roles.NameExistsAsync(request.Data.Name, request.Id, cancellationToken))
            throw new AppValidationException(new Dictionary<string, string[]> { [nameof(request.Data.Name)] = ["This role name already exists."] });
        request.Data.Adapt(role);
        var result = await roles.UpdateAsync(role);
        if (!result.Succeeded)
            throw new AppValidationException(result.Errors.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.Select(x => x.Description).ToArray()));
        return role.Adapt<RoleDto>();
    }
}

public sealed class DeleteRoleHandler(IRoleManagementService roles, NEXT.Application.Abstractions.Context.IDeletionAuditContext deletionAudit) : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roles.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.Id}' was not found.");
        if (role.IsSystem)
            throw new UnprocessableEntityException("System roles cannot be deleted.");

        using var audit = deletionAudit.Begin(NEXT.Application.Services.DeletionReason.Require(request.Reason));
        var result = await roles.DeleteAsync(role);
        if (!result.Succeeded)
            throw new UnprocessableEntityException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}

public sealed class GetRoleByIdHandler(IRoleManagementService roles) : IRequestHandler<GetRoleByIdQuery, RoleDto?>
{
    public async Task<RoleDto?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await roles.FindByIdAsync(request.Id, cancellationToken);
        return role?.Adapt<RoleDto>();
    }
}

public sealed class GetRolesHandler(IRoleManagementService roles) : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken) =>
        (await roles.GetAllAsync(cancellationToken)).Adapt<IReadOnlyList<RoleDto>>();
}
