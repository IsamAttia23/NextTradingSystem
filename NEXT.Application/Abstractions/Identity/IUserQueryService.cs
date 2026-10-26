using System.Security.Claims;
using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Identity;

/// <summary>Read operations for ApplicationUser identity information.</summary>
public interface IUserQueryService
{
    Task<ApplicationUser?> FindUserAsync(string userId);
    Task<ApplicationUser?> FindUserByEmailAsync(string email);
    Task<ApplicationUser?> FindUserByNameAsync(string userName);
    Task<bool> IsUserNameTakenAsync(string userName, string? excludingUserId = null);
    Task<bool> IsEmailTakenAsync(string email, string? excludingUserId = null);
    Task<IEnumerable<ApplicationUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);
    Task<IEnumerable<Claim>> GetUserClaims(ApplicationUser user);
    Task<IList<string>> GetUserRoles(ApplicationUser user);
}
