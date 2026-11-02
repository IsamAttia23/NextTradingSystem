using System.Security.Claims;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserByNameHandler(IUserQueryService users, UserDtoReader reader) : IRequestHandler<GetApplicationUserByNameQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetApplicationUserByNameQuery request, CancellationToken ct) =>
        await reader.ReadAsync(await users.FindUserByNameAsync(RequestValidation.Text(request.UserName, "UserName"), ct), ct);
}
