using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserByNameHandler(IUserQueryService users) : IRequestHandler<GetApplicationUserByNameQuery, UserDto?>
{
    public Task<UserDto?> Handle(GetApplicationUserByNameQuery request, CancellationToken ct) =>
        users.FindUserByNameAsync<UserDto>(RequestValidation.Text(request.UserName, "UserName"), ct);
}
