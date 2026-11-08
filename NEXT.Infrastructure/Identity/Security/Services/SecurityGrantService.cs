using NEXT.Infrastructure.Identity.Security.Storage;
using NEXT.Infrastructure.Identity.Security.Cryptography;
using NEXT.Infrastructure.Identity.Security.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using NEXT.Infrastructure.Localization;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity.Security.Services;

public sealed class SecurityGrantService(NEXTDbContext db, UserManager<ApplicationUser> users,
    SecurityTransactions transactions, TimeProvider clock, IOptions<AccountSecurityOptions> options,
    IStringLocalizer<SecurityMessages> text)
{
    public const string PasswordSetup = "PasswordSetup";
    public const string PasswordReset = "PasswordReset";

    public async Task<string> IssueAsync(ApplicationUser user, string purpose, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var previous = await db.AccountSecurityGrants
            .Where(x => x.UserId == user.Id && x.Purpose == purpose && x.UsedAt == null && x.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (var grant in previous) grant.UsedAt = now;
        var token = SecuritySecrets.NewToken();
        db.AccountSecurityGrants.Add(new AccountSecurityGrant
        {
            Id = Guid.NewGuid(), UserId = user.Id, TokenHash = SecuritySecrets.Hash(token), Purpose = purpose,
            CredentialStamp = user.SecurityStamp ?? "",
            ExpiresAt = now.AddMinutes(options.Value.GrantLifetimeMinutes)
        });
        await db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<UserOperationResult> UseAsync(string token, string purpose,
        Func<ApplicationUser, Task<UserOperationResult>> action, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 64) return Invalid();
        var hash = SecuritySecrets.Hash(token);
        var candidate = await db.AccountSecurityGrants.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (candidate is null) return Invalid();
        return await transactions.RunAsync("user:" + candidate.UserId, async () =>
        {
            var grant = await db.AccountSecurityGrants.FindAsync([candidate.Id], ct);
            if (grant is null) return Invalid();
            await db.Entry(grant).ReloadAsync(ct);
            var user = await users.FindByIdAsync(grant.UserId.ToString());
            if (user is not null) await db.Entry(user).ReloadAsync(ct);
            if (user is null || !user.IsActive || user.DeletedAt is not null || user.RequiresAdministratorUnlock ||
                user.LockoutEnd > clock.GetUtcNow() ||
                grant.Purpose != purpose || grant.UsedAt is not null || grant.ExpiresAt <= clock.GetUtcNow() ||
                grant.CredentialStamp != user.SecurityStamp)
                return Invalid();
            var result = await action(user);
            if (!result.Succeeded) return result;
            grant.UsedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return result;
        }, ct);
    }

    private UserOperationResult Invalid() => UserOperationResult.Failure(
        [new UserOperationError("InvalidSecurityToken", text["InvalidSecurityToken"])]);
}
