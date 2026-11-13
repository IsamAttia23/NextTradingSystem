using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Identity;

public interface IUserManagementService
{
    Task<UserOperationResult> CreateWithActivationOtpAsync(UserAccount user, CancellationToken ct = default);
    Task<LoginPreparation> PrepareLoginAsync(string userName, string password, bool issueSetupToken = false,
        CancellationToken ct = default);
    Task<UserOperationResult> CompleteFirstLoginAsync(string setupToken, string newPassword, CancellationToken ct = default);
    Task<UserOperationResult> ReissueActivationOtpAsync(Guid userId, CancellationToken ct = default);
    Task<UserOperationResult> SetActiveAsync(Guid userId, bool isActive, bool expectedIsActive, CancellationToken ct = default);
    Task<UserOperationResult> UnlockUserAsync(Guid userId, CancellationToken ct = default);
    Task<Guid> RequestPasswordResetAsync(string email, CancellationToken ct = default);
    Task<UserOperationResult> ResetPasswordAsync(string resetToken, string newPassword, CancellationToken ct = default);
}
