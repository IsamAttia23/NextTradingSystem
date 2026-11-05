using MapsterMapper;
using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Security.Roles;

public sealed class UpdateRoleHandler(
    IRoleManagementService roles,
    IEntityValidator<RoleUpdateDto> validator, IMapper mapper) : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    public async Task<RoleDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        validator.Validate(request.Data);
        RequestValidation.Identifier(request.Id);
        var input = request.Data with
        {
            Name = request.Data.Name.Trim()
        };
        var role = await roles.FindByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.Id}' was not found.");
        if (role.IsSystem && !string.Equals(role.Name, input.Name, StringComparison.Ordinal))
        {
            throw new UnprocessableEntityException("System roles cannot be renamed.");
        }
        if (await roles.NameExistsAsync(input.Name, request.Id, cancellationToken))
        {
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Data.Name)] = ["This role name already exists."]
            });
        }
        mapper.Map(input, role);
        var result = await roles.UpdateAsync(role, cancellationToken);
        if (!result.Succeeded)
        {
            ValidationErrors.ThrowIfAny(result.Errors.Select(x => (x.Code, x.Description)));
        }
        return mapper.Map<RoleDto>(role);
    }
}
