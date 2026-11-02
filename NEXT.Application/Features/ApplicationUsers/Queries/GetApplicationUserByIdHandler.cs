using System.Security.Claims;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserByIdHandler(IUserQueryService users, UserDtoReader reader) : IRequestHandler<GetApplicationUserByIdQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetApplicationUserByIdQuery request, CancellationToken ct) =>
        await reader.ReadAsync(await users.FindUserAsync(RequestValidation.Identifier(request.UserId).ToString(), ct), ct);
}
