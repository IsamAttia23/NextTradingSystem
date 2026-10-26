using Microsoft.AspNetCore.Identity;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.UsersManagment;

/// <summary>Implements identity mutations through ASP.NET Core Identity.</summary>
public sealed class UserCommandService(UserManager<ApplicationUser> userManager) : IUserCommandService
{
    public async Task<UserOperationResult> CreateUserAsync(ApplicationUser user, string password) =>
        ToApplicationResult(await userManager.CreateAsync(user, password));

    public async Task<UserOperationResult> UpdateUserAsync(ApplicationUser user) =>
        ToApplicationResult(await userManager.UpdateAsync(user));

    public async Task<UserOperationResult> DeleteUserAsync(ApplicationUser user) =>
        ToApplicationResult(await userManager.DeleteAsync(user));

    public async Task<UserOperationResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword) =>
        ToApplicationResult(await userManager.ChangePasswordAsync(user, currentPassword, newPassword));

    public async Task<UserOperationResult> SetUserNameAsync(ApplicationUser user, string userName) =>
        ToApplicationResult(await userManager.SetUserNameAsync(user, userName));

    public async Task<UserOperationResult> SetUserEmailAsync(ApplicationUser user, string email) =>
        ToApplicationResult(await userManager.SetEmailAsync(user, email));

    public async Task<UserOperationResult> SetPhoneNumberAsync(ApplicationUser user, string phoneNumber) =>
        ToApplicationResult(await userManager.SetPhoneNumberAsync(user, phoneNumber));

    public async Task<UserOperationResult> AddUserToRole(ApplicationUser user, string role) =>
        ToApplicationResult(await userManager.AddToRoleAsync(user, role));

    private static UserOperationResult ToApplicationResult(IdentityResult result) =>
        result.Succeeded
            ? UserOperationResult.Success
            : UserOperationResult.Failure(result.Errors.Select(error =>
                new UserOperationError(error.Code, error.Description)));
}
