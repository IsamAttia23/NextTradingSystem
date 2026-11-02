using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers;

public sealed class UserDtoReader(IUserQueryService users)
{
    public async Task<ApplicationUser> RequireAsync(Guid userId, CancellationToken ct)
    {
        RequestValidation.Identifier(userId, "UserId");
        return await users.FindUserAsync(userId.ToString(), ct) ?? throw new NotFoundException("User was not found.");
    }
}
