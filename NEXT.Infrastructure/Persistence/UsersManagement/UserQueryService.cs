using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;
using Mapster;
namespace NEXT.Infrastructure.Persistence.UsersManagement;

public sealed class UserQueryService(UserManager<ApplicationUser> users, TypeAdapterConfig mapping) : IUserQueryService
{
    public Task<ApplicationUser?> FindUserAsync(string userId, CancellationToken ct = default) =>
        Guid.TryParse(userId, out var id) ? StorageErrors.ExecuteAsync(() => users.Users.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct))
            : Task.FromResult<ApplicationUser?>(null);
    public Task<ApplicationUser?> FindUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = users.NormalizeEmail(email);
        return StorageErrors.ExecuteAsync(() => users.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalized, ct));
    }
    public Task<ApplicationUser?> FindUserByNameAsync(string userName, CancellationToken ct = default)
    {
        var normalized = users.NormalizeName(userName);
        return StorageErrors.ExecuteAsync(() => users.Users.FirstOrDefaultAsync(x => x.NormalizedUserName == normalized, ct));
    }
    public Task<bool> IsUserNameTakenAsync(string userName, string? excludingUserId = null, CancellationToken ct = default)
    {
        var normalized = users.NormalizeName(userName.Trim());
        Guid? excluded = Guid.TryParse(excludingUserId, out var id) ? id : null;
        return StorageErrors.ExecuteAsync(() => users.Users.IgnoreQueryFilters().AnyAsync(user =>
            user.NormalizedUserName == normalized && (!excluded.HasValue || user.Id != excluded.Value), ct));
    }
    public Task<TDto?> FindUserAsync<TDto>(string userId, CancellationToken ct = default) where TDto : class =>
        Guid.TryParse(userId, out var id) ? StorageErrors.ExecuteAsync(() => users.Users.AsNoTracking()
            .Where(x => x.Id == id && x.DeletedAt == null).ProjectToType<TDto>(mapping).FirstOrDefaultAsync(ct))
            : Task.FromResult<TDto?>(null);
    public Task<TDto?> FindUserByEmailAsync<TDto>(string email, CancellationToken ct = default) where TDto : class
    {
        var normalized = users.NormalizeEmail(email);
        return StorageErrors.ExecuteAsync(() => users.Users.AsNoTracking().Where(x => x.NormalizedEmail == normalized)
            .ProjectToType<TDto>(mapping).FirstOrDefaultAsync(ct));
    }
    public Task<TDto?> FindUserByNameAsync<TDto>(string userName, CancellationToken ct = default) where TDto : class
    {
        var normalized = users.NormalizeName(userName);
        return StorageErrors.ExecuteAsync(() => users.Users.AsNoTracking().Where(x => x.NormalizedUserName == normalized)
            .ProjectToType<TDto>(mapping).FirstOrDefaultAsync(ct));
    }
    public Task<bool> IsEmailTakenAsync(string email, string? excludingUserId = null, CancellationToken ct = default)
    {
        var normalized = users.NormalizeEmail(email.Trim());
        Guid? excluded = Guid.TryParse(excludingUserId, out var id) ? id : null;
        return StorageErrors.ExecuteAsync(() => users.Users.IgnoreQueryFilters().AnyAsync(user =>
            user.NormalizedEmail == normalized && (!excluded.HasValue || user.Id != excluded.Value), ct));
    }
    public async Task<IReadOnlyList<UserResponseDto>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        await StorageErrors.ExecuteAsync(() => users.Users.AsNoTracking().ProjectToType<UserResponseDto>(mapping)
            .ToListAsync(cancellationToken));
    public Task<bool> CheckPasswordAsync(ApplicationUser user, string password, CancellationToken ct = default) =>
        IdentityCall.CompleteAsync(() => users.CheckPasswordAsync(user, password), ct);
    public async Task<IEnumerable<Claim>> GetUserClaims(ApplicationUser user, CancellationToken ct = default) =>
        await IdentityCall.CompleteAsync(() => users.GetClaimsAsync(user), ct);
    public Task<IList<string>> GetUserRoles(ApplicationUser user, CancellationToken ct = default) =>
        IdentityCall.CompleteAsync(() => users.GetRolesAsync(user), ct);
}
