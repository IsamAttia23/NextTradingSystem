using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using NEXT.Domain.Entities;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Infrastructure.Persistence.UsersManagment
{
    public class UserManagmentServices : IUserManagementService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        protected IQueryable<ApplicationUser> Users => _userManager.Users;

        public UserManagmentServices(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IdentityResult> AddUserToRole(ApplicationUser user, string roles)
        {
            return await _userManager.AddToRoleAsync(user, roles);
        }

        public async Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword)
        {
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            return result;
        }

        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
        {
            var result = _userManager.CheckPasswordAsync(user, password);
            return result;
        }


        public async Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password)
        {
            var result = await _userManager.CreateAsync(user, password);
            return result;
        }

        public async Task<ApplicationUser?> FindUserAsync(string userId)
        {
            var result = await _userManager.FindByIdAsync(userId);
            return result;
        }

        public async Task<ApplicationUser?> FindUserByEmailAsync(string email)
        {
            var result = await _userManager.FindByEmailAsync(email);
            return result;
        }

        public async Task<ApplicationUser?> FindUserByNameAsync(string userName)
        {
            var result = await _userManager.FindByNameAsync(userName);
            return result;
        }

        public async Task<IEnumerable<Claim>> GetUserClaims(ApplicationUser user)
        {
            var claims = await _userManager.GetClaimsAsync(user);
            return claims;
        }

        public async Task<IList<string>> GetUserRoles(ApplicationUser user)
        {
            var result = await _userManager.GetRolesAsync(user);
            return result;
        }

        public async Task<IEnumerable<ApplicationUser>> GetUsersAsync()
        {
            var result = Users;
            return result;
        }

        public async Task<IdentityResult> SetPhoneNumberAsync(ApplicationUser user, string phoneNumber)
        {
            var phoneResult = await _userManager.SetPhoneNumberAsync(user, phoneNumber);
            return phoneResult;
        }

        public Task<IdentityResult> SetUserEmailAsync(ApplicationUser user, string email)
        {
            var result = _userManager.SetEmailAsync(user, email);
            return result;
        }

        public Task<IdentityResult> SetUserNameAsync(ApplicationUser user, string userName)
        {
            var result = _userManager.SetUserNameAsync(user, userName);
            return result;
        }

        public async Task<IdentityResult> UpdateUserAsync(ApplicationUser user)
        {
            var result = await _userManager.UpdateAsync(user);
            return result;
        }
    }
}
