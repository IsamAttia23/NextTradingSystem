using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserByEmailHandler(IUserQueryService users) : IRequestHandler<GetApplicationUserByEmailQuery, UserDto?>
{
    public Task<UserDto?> Handle(GetApplicationUserByEmailQuery request, CancellationToken ct) =>
        users.FindUserByEmailAsync<UserDto>(RequestValidation.Text(request.Email, "Email"), ct);
}
