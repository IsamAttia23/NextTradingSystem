using Microsoft.AspNetCore.Identity;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;
namespace NEXT.Infrastructure.Persistence.UsersManagement;
public sealed class UserCommandService(UserManager<ApplicationUser> users) : IUserCommandService
{
    public Task<UserOperationResult> CreateUserAsync(ApplicationUser user, string password, CancellationToken ct = default) =>
        ExecuteAsync(() => users.CreateAsync(user, password), ct);
    public Task<UserOperationResult> UpdateUserAsync(ApplicationUser user, CancellationToken ct = default) =>
        ExecuteAsync(() => users.UpdateAsync(user), ct);
    public Task<UserOperationResult> DeleteUserAsync(ApplicationUser user, CancellationToken ct = default) =>
        ExecuteAsync(() => users.DeleteAsync(user), ct);
    public Task<UserOperationResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword, CancellationToken ct = default) =>
        ExecuteAsync(() => users.ChangePasswordAsync(user, currentPassword, newPassword), ct);
    public Task<UserOperationResult> SetUserNameAsync(ApplicationUser user, string userName, CancellationToken ct = default) =>
        ExecuteAsync(() => users.SetUserNameAsync(user, userName), ct);
    public Task<UserOperationResult> SetUserEmailAsync(ApplicationUser user, string email, CancellationToken ct = default) =>
        ExecuteAsync(() => users.SetEmailAsync(user, email), ct);
    public Task<UserOperationResult> SetPhoneNumberAsync(ApplicationUser user, string phoneNumber, CancellationToken ct = default) =>
        ExecuteAsync(() => users.SetPhoneNumberAsync(user, phoneNumber), ct);
    public Task<UserOperationResult> AddUserToRole(ApplicationUser user, string role, CancellationToken ct = default) =>
        ExecuteAsync(() => users.AddToRoleAsync(user, role), ct);
    private static async Task<UserOperationResult> ExecuteAsync(Func<Task<IdentityResult>> action, CancellationToken ct)
    {
        var result = await IdentityCall.CompleteAsync(action, ct);
        return result.Succeeded ? UserOperationResult.Success : UserOperationResult.Failure(
            result.Errors.Select(x => new UserOperationError(x.Code, x.Description)));
    }
}
