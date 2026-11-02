using MapsterMapper;
using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Security.Roles;

public sealed class GetRoleByIdHandler(IRoleManagementService roles, IMapper mapper) : IRequestHandler<GetRoleByIdQuery, RoleDto?>
{
    public async Task<RoleDto?> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = await roles.FindByIdAsync(request.Id, cancellationToken);
        return role is null ? null : mapper.Map<RoleDto>(role);
    }
}
