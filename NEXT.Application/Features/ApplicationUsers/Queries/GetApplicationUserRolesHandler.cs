using System.Security.Claims;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserRolesHandler(IUserQueryService users, UserDtoReader reader) : IRequestHandler<GetApplicationUserRolesQuery, IList<string>>
{
    public async Task<IList<string>> Handle(GetApplicationUserRolesQuery request, CancellationToken ct) =>
        await users.GetUserRoles(await reader.RequireAsync(request.UserId, ct), ct);
}
