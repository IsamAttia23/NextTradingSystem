using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Identity;

/// <summary>Write operations for ApplicationUser identity management.</summary>
public interface IUserCommandService
{
    Task<UserOperationResult> CreateUserAsync(ApplicationUser user, string password);
    Task<UserOperationResult> UpdateUserAsync(ApplicationUser user);
    Task<UserOperationResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword);
    Task<UserOperationResult> SetUserNameAsync(ApplicationUser user, string userName);
    Task<UserOperationResult> SetUserEmailAsync(ApplicationUser user, string email);
    Task<UserOperationResult> SetPhoneNumberAsync(ApplicationUser user, string phoneNumber);
    Task<UserOperationResult> AddUserToRole(ApplicationUser user, string role);
}
