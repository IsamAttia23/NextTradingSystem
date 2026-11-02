using System.Security.Claims;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUsersHandler(IUserQueryService users) : IRequestHandler<GetApplicationUsersQuery, IEnumerable<UserResponseDto>>
{
    public async Task<IEnumerable<UserResponseDto>> Handle(GetApplicationUsersQuery request, CancellationToken ct) => await users.GetUsersAsync(ct);
}
