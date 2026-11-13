using NEXT.Infrastructure.Identity.Entities;
using NEXT.Infrastructure.Identity.Security.Storage;
using NEXT.Infrastructure.Identity.Security.Cryptography;
using NEXT.Infrastructure.Identity.Security.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using NEXT.Infrastructure.Localization;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity.Security.Services;

public sealed class EmailOtpService(NEXTDbContext db, UserManager<ApplicationUser> users,
    IEmailService email, SecuritySecrets secrets, SecurityRequestLimiter limiter,
    SecurityTransactions transactions, SecurityGrantService grants, TimeProvider clock,
    IOptions<AccountSecurityOptions> options, IRequestMetadataService metadata,
    IStringLocalizer<SecurityMessages> text) : IOtpService
{
    public async Task<Guid> SendAsync(Guid userId, OtpPurpose purpose, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(purpose))
            throw new ArgumentOutOfRangeException(nameof(purpose));

        return await transactions.RunAsync("user:" + userId, async () =>
        {
            if (!await limiter.AllowAsync("otp-send:" + userId + ":" + purpose, 3,
                TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(60), ct))
                throw new ConflictException("Wait before requesting another verification code.");

            var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("User not found.");
            await db.Entry(user).ReloadAsync(ct);
            if (!user.IsActive || user.DeletedAt is not null || user.RequiresAdministratorUnlock ||
                string.IsNullOrWhiteSpace(user.Email) ||
                (purpose == OtpPurpose.PasswordReset && (!user.EmailConfirmed || user.MustChangePassword)) ||
                (purpose == OtpPurpose.EmailVerification && !user.MustChangePassword))
                throw new ConflictException("The account is not eligible for this verification.");

            var now = clock.GetUtcNow();
            var activation = purpose == OtpPurpose.EmailVerification;
            var lifetimeMinutes = activation
                ? options.Value.ActivationOtpLifetimeMinutes : options.Value.OtpLifetimeMinutes;
            var previous = await db.OtpChallenges.Where(x => x.UserId == userId && x.Purpose == (int)purpose &&
                x.UsedAt == null && x.RevokedAt == null).ToListAsync(ct);
            foreach (var challenge in previous) challenge.RevokedAt = now;
            var code = SecuritySecrets.NewOtp();
            var row = new OtpChallenge
            {
                Id = Guid.NewGuid(), UserId = userId, Purpose = (int)purpose, Email = user.Email,
                CredentialStamp = user.SecurityStamp ?? "", CreatedAt = now,
                ExpiresAt = now.AddMinutes(lifetimeMinutes)
            };
            row.SecretDigest = Fingerprint(row, code);
            db.OtpChallenges.Add(row);
            await email.QueueAsync(userId, user.Email, text[activation ? "ActivationEmailSubject" : "OtpEmailSubject"],
                activation ? text["ActivationEmailBody", user.UserName ?? "", code, lifetimeMinutes]
                    : text["OtpEmailBody", code, lifetimeMinutes], row.ExpiresAt,
                row.CredentialStamp, ct, row.Id);
            await db.SaveChangesAsync(ct);
            return row.Id;
        }, ct);
    }

    public async Task<OtpVerification> VerifyAsync(Guid challengeId, string code, OtpPurpose purpose,
        CancellationToken ct = default)
    {
        if (code is null || code.Length > 32 || !Enum.IsDefined(purpose) ||
            !await limiter.AllowAsync("otp-verify:ip:" + (metadata.IpAddress ?? "internal"), 60,
                TimeSpan.FromMinutes(15), TimeSpan.Zero, ct)) return new(false);
        var candidate = await db.OtpChallenges.AsNoTracking().FirstOrDefaultAsync(x => x.Id == challengeId, ct);
        if (candidate is null) return new(false);
        return await transactions.RunAsync("user:" + candidate.UserId, async () =>
        {
            var row = await db.OtpChallenges.FindAsync([challengeId], ct);
            if (row is null) return new OtpVerification(false);
            await db.Entry(row).ReloadAsync(ct);
            var user = await users.FindByIdAsync(row.UserId.ToString());
            if (user is not null) await db.Entry(user).ReloadAsync(ct);
            var now = clock.GetUtcNow();
            if (row.Purpose != (int)purpose || row.UsedAt is not null || row.RevokedAt is not null ||
                row.ExpiresAt <= now || row.FailedAttempts >= 3 || user is null ||
                user.DeletedAt is not null || user.LockoutEnd > now || row.Email != user.Email ||
                (purpose == OtpPurpose.PasswordReset && (!user.EmailConfirmed || user.MustChangePassword)) ||
                (purpose == OtpPurpose.EmailVerification && !user.MustChangePassword))
                return new OtpVerification(false);

            var matches = SecuritySecrets.Matches(row.SecretDigest, Fingerprint(row, code));
            var validCode = code.Length == 5 && code.All(c => c is >= '0' and <= '9') && matches;
            if (!user.IsActive || user.RequiresAdministratorUnlock)
            {
                // A matching activation code can explain the lock, but never grants access.
                return new OtpVerification(false, AccountLocked: purpose == OtpPurpose.EmailVerification && validCode);
            }
            if (row.CredentialStamp != user.SecurityStamp) return new OtpVerification(false);
            if (!validCode)
            {
                row.FailedAttempts++;
                if (purpose == OtpPurpose.EmailVerification)
                {
                    user.AccessFailedCount++;
                    user.LastInvalidLoginAttempt = now.UtcDateTime;
                    if (user.AccessFailedCount >= 3) user.RequiresAdministratorUnlock = true;
                    UserManagementService.Ensure(await users.UpdateAsync(user));
                }
                if (row.FailedAttempts >= 3) row.RevokedAt = now;
                await db.SaveChangesAsync(ct);
                return new OtpVerification(false);
            }
            row.UsedAt = now;
            if (purpose == OtpPurpose.EmailVerification)
            {
                user.TemporaryPasswordUsedAt = now;
                user.AccessFailedCount = 0;
                UserManagementService.Ensure(await users.UpdateAsync(user));
            }
            var grantPurpose = purpose == OtpPurpose.EmailVerification
                ? SecurityGrantService.PasswordSetup : SecurityGrantService.PasswordReset;
            var grant = await grants.IssueAsync(user, grantPurpose, ct);
            return new OtpVerification(true, grant);
        }, ct);
    }

    private string Fingerprint(OtpChallenge row, string code) => secrets.Fingerprint(
        $"{row.Id:N}:{row.UserId:N}:{row.Purpose}:{row.CredentialStamp}:{row.Email}:{code}");
}
