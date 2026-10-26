using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Identity;

/// <summary>Write operations for ApplicationUser identity management.</summary>
public interface IUserCommandService
{
    Task<UserOperationResult> CreateUserAsync(ApplicationUser user, string password);
    Task<IdentityResult> UpdateUserAsync(ApplicationUser user);
    Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword);
    Task<IdentityResult> SetUserNameAsync(ApplicationUser user, string userName);
    Task<IdentityResult> SetUserEmailAsync(ApplicationUser user, string email);
    Task<IdentityResult> SetPhoneNumberAsync(ApplicationUser user, string phoneNumber);
    Task<IdentityResult> AddUserToRole(ApplicationUser user, string role);
}
