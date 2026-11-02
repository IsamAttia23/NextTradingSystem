using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Identity;

public interface IUserCommandService
{
    Task<UserOperationResult> CreateUserAsync(ApplicationUser user, string password, CancellationToken ct = default);
    Task<UserOperationResult> UpdateUserAsync(ApplicationUser user, CancellationToken ct = default);
    Task<UserOperationResult> DeleteUserAsync(ApplicationUser user, CancellationToken ct = default);
    Task<UserOperationResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<UserOperationResult> SetUserNameAsync(ApplicationUser user, string userName, CancellationToken ct = default);
    Task<UserOperationResult> SetUserEmailAsync(ApplicationUser user, string email, CancellationToken ct = default);
    Task<UserOperationResult> SetPhoneNumberAsync(ApplicationUser user, string phoneNumber, CancellationToken ct = default);
    Task<UserOperationResult> AddUserToRole(ApplicationUser user, string role, CancellationToken ct = default);
}
