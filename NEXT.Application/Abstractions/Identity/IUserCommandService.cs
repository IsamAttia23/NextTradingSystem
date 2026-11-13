using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Identity;

public interface IUserCommandService
{
    Task<UserOperationResult> CreateUserAsync(UserAccount user, string password, CancellationToken ct = default);
    Task<UserOperationResult> UpdateUserAsync(UserAccount user, CancellationToken ct = default);
    Task<UserOperationResult> DeleteUserAsync(UserAccount user, CancellationToken ct = default);
    Task<UserOperationResult> ChangePasswordAsync(UserAccount user, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<UserOperationResult> SetUserNameAsync(UserAccount user, string userName, CancellationToken ct = default);
    Task<UserOperationResult> SetUserEmailAsync(UserAccount user, string email, CancellationToken ct = default);
    Task<UserOperationResult> SetPhoneNumberAsync(UserAccount user, string phoneNumber, CancellationToken ct = default);
    Task<UserOperationResult> AddUserToRole(UserAccount user, string role, CancellationToken ct = default);
}
