using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.UsersManagment;

/// <summary>Implements identity reads through ASP.NET Core Identity.</summary>
public sealed class UserQueryService(UserManager<ApplicationUser> userManager) : IUserQueryService
{
    public Task<ApplicationUser?> FindUserAsync(string userId) =>
        userManager.FindByIdAsync(userId);

    public Task<ApplicationUser?> FindUserByEmailAsync(string email) =>
        userManager.FindByEmailAsync(email);

    public Task<ApplicationUser?> FindUserByNameAsync(string userName) =>
        userManager.FindByNameAsync(userName);

    public async Task<bool> IsUserNameTakenAsync(string userName, string? excludingUserId = null)
    {
        var user = await userManager.FindByNameAsync(userName.Trim());
        return user is not null && !string.Equals(user.Id.ToString(), excludingUserId, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsEmailTakenAsync(string email, string? excludingUserId = null)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        return user is not null && !string.Equals(user.Id.ToString(), excludingUserId, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IEnumerable<ApplicationUser>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        await userManager.Users.ToListAsync(cancellationToken);

    public Task<bool> CheckPasswordAsync(ApplicationUser user, string password) =>
        userManager.CheckPasswordAsync(user, password);

    public async Task<IEnumerable<Claim>> GetUserClaims(ApplicationUser user) =>
        await userManager.GetClaimsAsync(user);

    public async Task<IList<string>> GetUserRoles(ApplicationUser user) =>
        await userManager.GetRolesAsync(user);
}
