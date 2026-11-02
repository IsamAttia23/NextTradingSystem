using System.Security.Claims;
using MediatR;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers.Queries;

public sealed class CheckApplicationUserPasswordHandler(IUserQueryService users, UserDtoReader reader) : IRequestHandler<CheckApplicationUserPasswordQuery, bool>
{
    public async Task<bool> Handle(CheckApplicationUserPasswordQuery request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Password)) RequestValidation.Fail("Password", "A password is required.");
        return await users.CheckPasswordAsync(await reader.RequireAsync(request.UserId, ct), request.Password, ct);
    }
}
