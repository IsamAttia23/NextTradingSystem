using NEXT.Infrastructure.Identity.Entities;
using NEXT.Infrastructure.Identity.Security.Storage;
using NEXT.Infrastructure.Identity.Security.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using NEXT.Infrastructure.Localization;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity.Security.Services;

public sealed class UserManagementService(NEXTDbContext db, UserManager<ApplicationUser> users,
    IOtpService otp, SecurityTransactions transactions,
    SecurityGrantService grants, SecurityRequestLimiter limiter, TimeProvider clock,
    IOptions<AccountSecurityOptions> options, ICurrentUserService currentUser,
    IRequestMetadataService metadata, IConfiguration configuration,
    IStringLocalizer<SecurityMessages> text, IAdministratorLookup administrators) : IUserManagementService
{
    public async Task<UserOperationResult> CreateWithActivationOtpAsync(UserAccount account, CancellationToken ct = default)
    {
        if (account.Id == Guid.Empty) account.Id = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = account.Id,
            UserName = account.UserName,
            Email = account.Email,
            PhoneNumber = account.PhoneNumber,
            FullName = account.FullName,
            IsActive = account.IsActive,
            BranchId = account.BranchId,
            AvatarUrl = account.AvatarUrl,
            CreatedOn = account.CreatedOn
        };
        return await transactions.RunAsync("user:" + user.Id, async () =>
        {
            PrepareActivation(user);
            var result = await users.CreateAsync(user);
            if (!result.Succeeded) return ConvertResult(result);
            if (user.IsActive) await otp.SendAsync(user.Id, OtpPurpose.EmailVerification, ct);
            return UserOperationResult.Success;
        }, ct);
    }

    public async Task<LoginPreparation> PrepareLoginAsync(string userName, string password,
        bool issueSetupToken = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password) || password.Length > 1024 ||
            userName.Length > 256 || !await AllowClientAsync("login", 60, ct))
            return new(LoginOutcome.Failed);

        var candidate = await users.FindByNameAsync(userName.Trim());

        if (candidate is null)
            return new(LoginOutcome.Failed);

        if (candidate.MustChangePassword)
            return await PrepareActivationLoginAsync(candidate, password, issueSetupToken, ct);

        return await transactions.RunAsync("user:" + candidate.Id, async () =>
        {
            await db.Entry(candidate).ReloadAsync(ct);
            if ((!candidate.IsActive || candidate.RequiresAdministratorUnlock) && candidate.DeletedAt is null)
                return new LoginPreparation(await users.CheckPasswordAsync(candidate, password)
                    ? LoginOutcome.AccountLocked : LoginOutcome.Failed);

            if (!Eligible(candidate))
                return new LoginPreparation(LoginOutcome.Failed);

            if (!await users.CheckPasswordAsync(candidate, password))
            {
                candidate.AccessFailedCount++;
                candidate.LastInvalidLoginAttempt = clock.GetUtcNow().UtcDateTime;

                if (candidate.AccessFailedCount >= 3)
                    candidate.RequiresAdministratorUnlock = true;
                Ensure(await users.UpdateAsync(candidate));
                return new LoginPreparation(LoginOutcome.Failed);
            }

            candidate.AccessFailedCount = 0;
            Ensure(await users.UpdateAsync(candidate));
            return new LoginPreparation(LoginOutcome.Succeeded);
        }, ct);
    }

    public Task<UserOperationResult> CompleteFirstLoginAsync(string setupToken, string newPassword,
        CancellationToken ct = default) => grants.UseAsync(setupToken, SecurityGrantService.PasswordSetup,
            user => SetPasswordAsync(user, newPassword, true, ct), ct);

    public async Task<UserOperationResult> SetActiveAsync(Guid userId, bool isActive, bool expectedIsActive, CancellationToken ct = default)
    {
        await RequireAdministratorAsync(ct);
        if (currentUser.UserId == userId)
            throw new UnprocessableEntityException("CannotChangeOwnActivation");
        return await transactions.RunAsync("administrator-activation", async () =>
        {
            var user = await RequireUserAsync(userId, ct);
            if (user.IsActive != expectedIsActive) throw new ConflictException("UserActivationChanged");
            if (user.IsActive == isActive) return UserOperationResult.Success;
            if (!isActive && await administrators.IsLastActiveAsync(userId, ct))
                throw new UnprocessableEntityException("CannotDeactivateLastAdministrator");
            user.IsActive = isActive;
            // Old sessions remain invalid even after a later reactivation.
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            Ensure(await users.UpdateAsync(user));
            return UserOperationResult.Success;
        }, ct);
    }

    public async Task<UserOperationResult> UnlockUserAsync(Guid userId, CancellationToken ct = default)
    {
        await RequireAdministratorAsync(ct);
        return await transactions.RunAsync("user:" + userId, async () =>
        {
            var user = await RequireUserAsync(userId, ct);
            user.RequiresAdministratorUnlock = false;
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            return ConvertResult(await users.UpdateAsync(user));
        }, ct);
    }

    public async Task<UserOperationResult> ReissueActivationOtpAsync(Guid userId, CancellationToken ct = default)
    {
        await RequireAdministratorAsync(ct);
        return await transactions.RunAsync("user:" + userId, async () =>
        {
            var user = await RequireUserAsync(userId, ct);
            if (!user.MustChangePassword || !Eligible(user))
                return Failure("ActivationNotAvailable", "The account is not eligible for activation.");
            PrepareActivation(user);
            if (await users.HasPasswordAsync(user)) Ensure(await users.RemovePasswordAsync(user));
            Ensure(await users.UpdateSecurityStampAsync(user));
            Ensure(await users.UpdateAsync(user));
            await otp.SendAsync(user.Id, OtpPurpose.EmailVerification, ct);
            return UserOperationResult.Success;
        }, ct);
    }

    private async Task<LoginPreparation> PrepareActivationLoginAsync(ApplicationUser user, string code,
        bool issueSetupToken, CancellationToken ct)
    {
        if (user.DeletedAt is not null || user.LockoutEnd > clock.GetUtcNow()) return new(LoginOutcome.Failed);
        // The first stage only selects the flow; verification happens once in the shared OTP service.
        if (!issueSetupToken) return new(LoginOutcome.RequiresPasswordChange);
        var challenge = await db.OtpChallenges.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.Purpose == (int)OtpPurpose.EmailVerification &&
                x.UsedAt == null && x.RevokedAt == null)
            .OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (challenge is null) return new(LoginOutcome.Failed);
        var verified = await otp.VerifyAsync(challenge.Value, code, OtpPurpose.EmailVerification, ct);
        if (verified.AccountLocked) return new(LoginOutcome.AccountLocked);
        return verified.Succeeded
            ? new(LoginOutcome.RequiresPasswordChange, verified.Grant)
            : new(LoginOutcome.Failed);
    }

    public async Task<Guid> RequestPasswordResetAsync(string emailAddress, CancellationToken ct = default)
    {
        var unknownChallenge = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(emailAddress) || emailAddress.Length > 256 ||
            !await AllowClientAsync("reset-request", 30, ct)) return unknownChallenge;
        var normalized = users.NormalizeEmail(emailAddress.Trim());
        if (!await limiter.AllowAsync("reset-email:" + normalized, 3, TimeSpan.FromMinutes(15),
            TimeSpan.FromSeconds(60), ct)) return unknownChallenge;
        var user = await users.FindByEmailAsync(emailAddress.Trim());
        if (user is null || !Eligible(user) || !user.EmailConfirmed || user.MustChangePassword)
            return unknownChallenge;
        try { return await otp.SendAsync(user.Id, OtpPurpose.PasswordReset, ct); }
        catch (ConflictException) { return unknownChallenge; }
    }

    public Task<UserOperationResult> ResetPasswordAsync(string resetToken, string newPassword,
        CancellationToken ct = default) => grants.UseAsync(resetToken, SecurityGrantService.PasswordReset,
            user => SetPasswordAsync(user, newPassword, false, ct), ct);

    private async Task<UserOperationResult> SetPasswordAsync(ApplicationUser user, string password,
        bool firstLogin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length > 128 || firstLogin != user.MustChangePassword)
            return Failure("InvalidPasswordSetup", "The password change is not valid.");
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded) return ConvertResult(result);
        user.MustChangePassword = false;
        user.TemporaryPasswordExpiresAt = null;
        user.AccessFailedCount = 0;
        if (firstLogin) user.EmailConfirmed = true;
        // Identity rotates the security stamp, invalidating previous sessions and grants.
        Ensure(await users.UpdateAsync(user));
        await db.SaveChangesAsync(ct);
        return UserOperationResult.Success with { RequiresSignIn = true };
    }

    private void PrepareActivation(ApplicationUser user)
    {
        user.MustChangePassword = true;
        user.TemporaryPasswordUsedAt = null;
        user.TemporaryPasswordExpiresAt = clock.GetUtcNow().AddMinutes(options.Value.ActivationOtpLifetimeMinutes);
        user.EmailConfirmed = false;
        user.LockoutEnabled = true;
    }

    private async Task<ApplicationUser> RequireUserAsync(Guid id, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("User not found.");
        await db.Entry(user).ReloadAsync(ct);
        if (user.DeletedAt is not null) throw new NotFoundException("User not found.");
        return user;
    }

    private async Task RequireAdministratorAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } id) throw new UnprocessableEntityException("Administrator access is required.");
        var user = await RequireUserAsync(id, ct);
        if (!Eligible(user) || user.MustChangePassword) throw new UnprocessableEntityException("Administrator access is required.");
        var roles = configuration.GetSection("Security:AdministratorRoles").Get<string[]>() ?? ["Admin"];
        foreach (var role in roles) if (await users.IsInRoleAsync(user, role)) return;
        throw new UnprocessableEntityException("Administrator access is required.");
    }

    private Task<bool> AllowClientAsync(string operation, int limit, CancellationToken ct) => limiter.AllowAsync(
        operation + ":ip:" + (metadata.IpAddress ?? "internal"), limit, TimeSpan.FromMinutes(15), TimeSpan.Zero, ct);

    private bool Eligible(ApplicationUser user) => user.IsActive && user.DeletedAt is null &&
        !user.RequiresAdministratorUnlock && (user.LockoutEnd is null || user.LockoutEnd <= clock.GetUtcNow());

    internal static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new ConflictException("The account changed concurrently or failed identity validation.");
    }

    private static UserOperationResult ConvertResult(IdentityResult result) => result.Succeeded
        ? UserOperationResult.Success
        : UserOperationResult.Failure(result.Errors.Select(x => new UserOperationError(x.Code, x.Description)));

    private UserOperationResult Failure(string code, string description) =>
        UserOperationResult.Failure([new UserOperationError(code, text[code].ResourceNotFound ? description : text[code])]);
}
