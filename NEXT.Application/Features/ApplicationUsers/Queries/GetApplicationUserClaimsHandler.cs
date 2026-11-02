using System.Security.Claims;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserClaimsHandler(IUserQueryService users, UserDtoReader reader) : IRequestHandler<GetApplicationUserClaimsQuery, IEnumerable<Claim>>
{
    public async Task<IEnumerable<Claim>> Handle(GetApplicationUserClaimsQuery request, CancellationToken ct) =>
        await users.GetUserClaims(await reader.RequireAsync(request.UserId, ct), ct);
}
