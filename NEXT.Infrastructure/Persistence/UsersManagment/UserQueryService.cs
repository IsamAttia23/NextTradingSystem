using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;
namespace NEXT.Infrastructure.Persistence.UsersManagment;
public sealed class UserQueryService(UserManager<ApplicationUser> users) : IUserQueryService
{
    public Task<ApplicationUser?> FindUserAsync(string userId, CancellationToken ct = default) =>
        Guid.TryParse(userId, out var id) ? users.Users.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct)
            : Task.FromResult<ApplicationUser?>(null);
    public Task<ApplicationUser?> FindUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = users.NormalizeEmail(email);
        return users.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalized, ct);
    }
    public Task<ApplicationUser?> FindUserByNameAsync(string userName, CancellationToken ct = default)
    {
        var normalized = users.NormalizeName(userName);
        return users.Users.FirstOrDefaultAsync(x => x.NormalizedUserName == normalized, ct);
    }
    public async Task<bool> IsUserNameTakenAsync(string userName, string? excludingUserId = null, CancellationToken ct = default)
    {
        var user = await FindUserByNameAsync(userName.Trim(), ct);
        return user is not null && !string.Equals(user.Id.ToString(), excludingUserId, StringComparison.OrdinalIgnoreCase);
    }
    public async Task<bool> IsEmailTakenAsync(string email, string? excludingUserId = null, CancellationToken ct = default)
    {
        var user = await FindUserByEmailAsync(email.Trim(), ct);
        return user is not null && !string.Equals(user.Id.ToString(), excludingUserId, StringComparison.OrdinalIgnoreCase);
    }
    public async Task<IReadOnlyList<UserResponseDto>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        await users.Users.AsNoTracking().Select(x => new UserResponseDto(x.Id, x.FullName, x.UserName, x.Email, x.IsActive, x.BranchId))
            .ToListAsync(cancellationToken);
    public Task<bool> CheckPasswordAsync(ApplicationUser user, string password, CancellationToken ct = default) =>
        IdentityCall.CompleteAsync(() => users.CheckPasswordAsync(user, password), ct);
    public async Task<IEnumerable<Claim>> GetUserClaims(ApplicationUser user, CancellationToken ct = default) =>
        await IdentityCall.CompleteAsync(() => users.GetClaimsAsync(user), ct);
    public Task<IList<string>> GetUserRoles(ApplicationUser user, CancellationToken ct = default) =>
        IdentityCall.CompleteAsync(() => users.GetRolesAsync(user), ct);
}
