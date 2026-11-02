using NEXT.Application.DTOs.ApplicationUsers;
using System.Security.Claims;
using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Identity;

public interface IUserQueryService
{
    Task<ApplicationUser?> FindUserAsync(string userId, CancellationToken ct = default);
    Task<ApplicationUser?> FindUserByEmailAsync(string email, CancellationToken ct = default);
    Task<ApplicationUser?> FindUserByNameAsync(string userName, CancellationToken ct = default);
    Task<TDto?> FindUserAsync<TDto>(string userId, CancellationToken ct = default) where TDto : class;
    Task<TDto?> FindUserByEmailAsync<TDto>(string email, CancellationToken ct = default) where TDto : class;
    Task<TDto?> FindUserByNameAsync<TDto>(string userName, CancellationToken ct = default) where TDto : class;
    Task<bool> IsUserNameTakenAsync(string userName, string? excludingUserId = null, CancellationToken ct = default);
    Task<bool> IsEmailTakenAsync(string email, string? excludingUserId = null, CancellationToken ct = default);
    Task<IReadOnlyList<UserResponseDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> CheckPasswordAsync(ApplicationUser user, string password, CancellationToken ct = default);
    Task<IEnumerable<Claim>> GetUserClaims(ApplicationUser user, CancellationToken ct = default);
    Task<IList<string>> GetUserRoles(ApplicationUser user, CancellationToken ct = default);
}
