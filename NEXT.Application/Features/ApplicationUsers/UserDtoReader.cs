using MapsterMapper;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Exceptions;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;

namespace NEXT.Application.Features.ApplicationUsers;

public sealed class UserDtoReader(IUserQueryService users, IMapper mapper)
{
    public async Task<ApplicationUser> RequireAsync(Guid userId, CancellationToken ct)
    {
        RequestValidation.Identifier(userId, "UserId");
        return await users.FindUserAsync(userId.ToString(), ct) ?? throw new NotFoundException("User was not found.");
    }
    public async Task<UserDto?> ReadAsync(ApplicationUser? user, CancellationToken ct)
    {
        if (user is null) return null;
        return mapper.Map<UserDto>(user) with { Roles = (await users.GetUserRoles(user, ct)).ToArray() };
    }
}
