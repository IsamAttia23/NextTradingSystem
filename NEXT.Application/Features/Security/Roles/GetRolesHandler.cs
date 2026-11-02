using MapsterMapper;
using Mapster;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.Security.Roles;

public sealed class GetRolesHandler(IRoleManagementService roles, IMapper mapper) : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken) =>
        mapper.Map<IReadOnlyList<RoleDto>>(await roles.GetAllAsync(cancellationToken));
}
