using Microsoft.AspNetCore.Identity;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;

namespace NEXT.Infrastructure.Persistence.UsersManagement;

public sealed class UserCommandService(UserManager<ApplicationUser> users, IUserManagementService management) : IUserCommandService
{
    public Task<UserOperationResult> CreateUserAsync(UserAccount user, string password, CancellationToken ct = default) =>
        management.CreateWithActivationOtpAsync(user, ct);

    public Task<UserOperationResult> UpdateUserAsync(UserAccount user, CancellationToken ct = default) =>
        ExecuteAsync(user.Id, account =>
        {
            account.FullName = user.FullName;
            account.UserName = user.UserName;
            account.AvatarUrl = user.AvatarUrl;
            account.BranchId = user.BranchId;
            account.IsActive = user.IsActive;
            account.UpdatedOn = user.UpdatedOn;
            return users.UpdateAsync(account);
        }, ct);

    public Task<UserOperationResult> DeleteUserAsync(UserAccount user, CancellationToken ct = default) =>
        ExecuteAsync(user.Id, account =>
        {
            account.DeletionReason = user.DeletionReason;
            return users.DeleteAsync(account);
        }, ct);

    public Task<UserOperationResult> ChangePasswordAsync(UserAccount user, string currentPassword, string newPassword, CancellationToken ct = default) =>
        ExecuteAsync(user.Id, account => users.ChangePasswordAsync(account, currentPassword, newPassword), ct);
    public Task<UserOperationResult> SetUserNameAsync(UserAccount user, string userName, CancellationToken ct = default) =>
        ExecuteAsync(user.Id, account => users.SetUserNameAsync(account, userName), ct);
    public Task<UserOperationResult> SetUserEmailAsync(UserAccount user, string email, CancellationToken ct = default) =>
        ExecuteAsync(user.Id, account => users.SetEmailAsync(account, email), ct);
    public Task<UserOperationResult> SetPhoneNumberAsync(UserAccount user, string phoneNumber, CancellationToken ct = default) =>
        ExecuteAsync(user.Id, account => users.SetPhoneNumberAsync(account, phoneNumber), ct);
    public Task<UserOperationResult> AddUserToRole(UserAccount user, string role, CancellationToken ct = default) =>
        ExecuteAsync(user.Id, account => users.AddToRoleAsync(account, role), ct);

    private async Task<UserOperationResult> ExecuteAsync(Guid id, Func<ApplicationUser, Task<IdentityResult>> action, CancellationToken ct)
    {
        var account = await IdentityCall.RequireAccountAsync(users, id, ct);
        var result = await IdentityCall.CompleteAsync(() => action(account), ct);
        return result.Succeeded ? UserOperationResult.Success : UserOperationResult.Failure(
            result.Errors.Select(x => new UserOperationError(x.Code, x.Description)));
    }
}
