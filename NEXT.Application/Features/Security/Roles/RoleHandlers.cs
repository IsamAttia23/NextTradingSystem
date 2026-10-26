using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Security.Roles;

public sealed class CreateRoleHandler(IRoleManagementService roles) : IRequestHandler<CreateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Data.Name))
            throw new AppValidationException(new Dictionary<string, string[]> { [nameof(request.Data.Name)] = ["Role name is required."] });
        if (await roles.NameExistsAsync(request.Data.Name, cancellationToken: cancellationToken))
            throw new AppValidationException(new Dictionary<string, string[]> { [nameof(request.Data.Name)] = ["This role name already exists."] });

        var role = request.Data.Adapt<Role>();
        var result = await roles.CreateAsync(role);
        if (!result.Succeeded)
            throw new AppValidationException(result.Errors.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.Select(x => x.Description).ToArray()));
        return role.Adapt<RoleDto>();
    }
}
public sealed class UpdateRoleHandler(IRoleManagementService roles) : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roles.FindByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Role '{request.Id}' was not found.");
        if (await roles.NameExistsAsync(request.Data.Name, request.Id, cancellationToken))
            throw new AppValidationException(new Dictionary<string, string[]> { [nameof(request.Data.Name)] = ["This role name already exists."] });
        request.Data.Adapt(role);
        var result = await roles.UpdateAsync(role);
        if (!result.Succeeded)
            throw new AppValidationException(result.Errors.GroupBy(x => x.Code).ToDictionary(g => g.Key, g => g.Select(x => x.Description).ToArray()));
        return role.Adapt<RoleDto>();
    }
}
public sealed class DeleteRoleHandler(IRoleManagementService roles) : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await roles.FindByIdAsync(request.Id, cancellationToken) ?? throw new NotFoundException($"Role '{request.Id}' was not found.");
        if (role.IsSystem) throw new UnprocessableEntityException("System roles cannot be deleted.");
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
