using MapsterMapper;
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
    IEntityValidator<RoleCreationDto> validator, IMapper mapper) : IRequestHandler<CreateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        validator.Validate(request.Data);
        if (await roles.NameExistsAsync(request.Data.Name, cancellationToken: cancellationToken))
            throw new AppValidationException(new Dictionary<string, string[]> { [nameof(request.Data.Name)] = ["This role name already exists."] });

        var role = mapper.Map<Role>(request.Data);
        role.IsSystem = false;
        var result = await roles.CreateAsync(role, cancellationToken);
        if (!result.Succeeded)
            ValidationErrors.ThrowIfAny(result.Errors.Select(x => (x.Code, x.Description)));
        return mapper.Map<RoleDto>(role);
    }
}
