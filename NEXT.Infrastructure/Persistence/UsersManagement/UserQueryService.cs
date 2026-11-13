using NEXT.Infrastructure.Identity.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Domain.Entities;
using Mapster;
namespace NEXT.Infrastructure.Persistence.UsersManagement;

public sealed class UserQueryService(UserManager<ApplicationUser> users, TypeAdapterConfig mapping, NEXT.Application.Abstractions.Context.ICurrentUserService current) : IUserQueryService
{
    public Task<UserAccount?> FindUserAsync(string userId, CancellationToken ct = default) =>
        Guid.TryParse(userId, out var id) ? ReadAccountAsync(x => x.Id == id && x.DeletedAt == null, ct)
            : Task.FromResult<UserAccount?>(null);
    public Task<UserAccount?> FindUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = users.NormalizeEmail(email);
        return ReadAccountAsync(x => x.NormalizedEmail == normalized, ct);
    }
    public Task<UserAccount?> FindUserByNameAsync(string userName, CancellationToken ct = default)
    {
        var normalized = users.NormalizeName(userName);
        return ReadAccountAsync(x => x.NormalizedUserName == normalized, ct);
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
        await StorageErrors.ExecuteAsync(() => users.Users.AsNoTracking().Where(x => x.DeletedAt == null && (!current.UserId.HasValue || x.Id != current.UserId.Value))
            .OrderBy(x => x.FullName).ThenBy(x => x.Id).ProjectToType<UserResponseDto>(mapping)
            .ToListAsync(cancellationToken));
    private async Task<UserAccount?> ReadAccountAsync(System.Linq.Expressions.Expression<Func<ApplicationUser, bool>> predicate, CancellationToken ct)
    {
        var account = await StorageErrors.ExecuteAsync(() => users.Users.FirstOrDefaultAsync(predicate, ct));
        return account?.Adapt<UserAccount>(mapping);
    }

    public Task<bool> CheckPasswordAsync(UserAccount user, string password, CancellationToken ct = default) =>
        WithAccountAsync(user.Id, account => users.CheckPasswordAsync(account, password), ct);
    public async Task<IEnumerable<Claim>> GetUserClaims(UserAccount user, CancellationToken ct = default) =>
        await WithAccountAsync(user.Id, users.GetClaimsAsync, ct);
    public Task<IList<string>> GetUserRoles(UserAccount user, CancellationToken ct = default) =>
        WithAccountAsync(user.Id, users.GetRolesAsync, ct);

    private async Task<T> WithAccountAsync<T>(Guid id, Func<ApplicationUser, Task<T>> operation, CancellationToken ct)
    {
        var account = await IdentityCall.RequireAccountAsync(users, id, ct);
        return await IdentityCall.CompleteAsync(() => operation(account), ct);
    }
}
