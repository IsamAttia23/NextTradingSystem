using System.Security.Claims;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserByEmailHandler(IUserQueryService users, UserDtoReader reader) : IRequestHandler<GetApplicationUserByEmailQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetApplicationUserByEmailQuery request, CancellationToken ct) =>
        await reader.ReadAsync(await users.FindUserByEmailAsync(RequestValidation.Text(request.Email, "Email"), ct), ct);
}
