using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class GetApplicationUserByIdHandler(IUserQueryService users) : IRequestHandler<GetApplicationUserByIdQuery, UserDto?>
{
    public Task<UserDto?> Handle(GetApplicationUserByIdQuery request, CancellationToken ct) =>
        users.FindUserAsync<UserDto>(RequestValidation.Identifier(request.UserId).ToString(), ct);
}
