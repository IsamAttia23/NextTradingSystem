using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Persistence.UsersManagment
{
    public interface IUserManagmentServices
    {
        Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password);
        Task<ApplicationUser> FindUserByEmailAsync(string email);
        Task<ApplicationUser> FindUserAsync(string userId);
        Task<ApplicationUser> FindUserByNameAsync(string userName);
        Task<IEnumerable<ApplicationUser>> GetUsersAsync();
        Task<IdentityResult> UpdateUserAsync(ApplicationUser user);
        Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword);
        Task<IdentityResult> SetUserNameAsync(ApplicationUser user, string userName);
        Task<IdentityResult> SetUserEmailAsync(ApplicationUser user, string email);
        Task<IdentityResult> SetPhoneNumberAsync(ApplicationUser user, string phoneNumber);
        Task<bool> CheckPasswordAsync(ApplicationUser user, string password);
        Task<IdentityResult> AddUserToRole(ApplicationUser user,string role);
        Task<IEnumerable<Claim>> GetUserClaims(ApplicationUser user);
        Task<IList<string>> GetUserRoles(ApplicationUser user);
    }
}
