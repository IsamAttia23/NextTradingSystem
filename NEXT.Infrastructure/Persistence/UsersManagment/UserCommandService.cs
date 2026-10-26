using Microsoft.AspNetCore.Identity;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.UsersManagment;

/// <summary>Implements identity mutations through ASP.NET Core Identity.</summary>
public sealed class UserCommandService(UserManager<ApplicationUser> userManager) : IUserCommandService
{
    public Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password) =>
        userManager.CreateAsync(user, password);

    public Task<IdentityResult> UpdateUserAsync(ApplicationUser user) =>
        userManager.UpdateAsync(user);

    public Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword) =>
        userManager.ChangePasswordAsync(user, currentPassword, newPassword);

    public Task<IdentityResult> SetUserNameAsync(ApplicationUser user, string userName) =>
        userManager.SetUserNameAsync(user, userName);

    public Task<IdentityResult> SetUserEmailAsync(ApplicationUser user, string email) =>
        userManager.SetEmailAsync(user, email);

    public Task<IdentityResult> SetPhoneNumberAsync(ApplicationUser user, string phoneNumber) =>
        userManager.SetPhoneNumberAsync(user, phoneNumber);

    public Task<IdentityResult> AddUserToRole(ApplicationUser user, string role) =>
        userManager.AddToRoleAsync(user, role);
}
