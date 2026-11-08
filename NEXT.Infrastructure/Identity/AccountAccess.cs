using NEXT.Infrastructure.Identity.Security.Services;
using NEXT.Infrastructure.Identity.Security.Storage;
using Microsoft.AspNetCore.Identity;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Authentication;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NEXT.Infrastructure.Persistence;
namespace NEXT.Infrastructure.Identity;

public sealed class AccountAccess(SignInManager<ApplicationUser> signIn, ILoginAccessPolicy policy,
    ICurrentUserService current, IEntityValidator<LoginDto> loginValidator,
    IEntityValidator<VerifyLoginDto> verifyValidator, IUserManagementService management,
    SecurityTransactions transactions, NEXTDbContext db) : IAccountAccess
{
    public Task<LoginOutcome> LoginAsync(LoginDto input) => StorageErrors.ExecuteAsync(() => LoginCoreAsync(input));
    private async Task<LoginOutcome> LoginCoreAsync(LoginDto input)
    {
        loginValidator.Validate(input);
        var user = await signIn.UserManager.FindByNameAsync(input.UserName);

        if (user is null)
        {
            return LoginOutcome.Failed;
        }

        var prepared = await management.PrepareLoginAsync(input.UserName, input.Password);

        if (prepared.Outcome != LoginOutcome.Succeeded)
            return prepared.Outcome;

        if (!await policy.CanSignInAsync(user.Id))
            return LoginOutcome.Failed;

        var result = await signIn.PasswordSignInAsync(user, input.Password, input.RememberMe, false);
        return result.Succeeded ? LoginOutcome.Succeeded
            : result.RequiresTwoFactor ? LoginOutcome.RequiresTwoFactor : LoginOutcome.Failed;
    }
    public Task<bool> HasChallengeAsync() => StorageErrors.ExecuteAsync(async () => await signIn.GetTwoFactorAuthenticationUserAsync() is not null);
    public Task<LoginOutcome> VerifyAsync(VerifyLoginDto input) => StorageErrors.ExecuteAsync(() => VerifyCoreAsync(input));
    private async Task<LoginOutcome> VerifyCoreAsync(VerifyLoginDto input)
    {
        verifyValidator.Validate(input);
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null || !await policy.CanSignInAsync(user.Id))
        {
            return LoginOutcome.MissingChallenge;
        }
        return await transactions.RunAsync("user:" + user.Id, async () =>
        {
            await db.Entry(user).ReloadAsync();
            if (!await policy.CanSignInAsync(user.Id)) return LoginOutcome.Failed;
            var attemptsBefore = user.AccessFailedCount;
            var result = input.UseRecoveryCode
                ? await signIn.TwoFactorRecoveryCodeSignInAsync(input.Code.Replace(" ", ""))
                : await signIn.TwoFactorAuthenticatorSignInAsync(input.Code.Replace(" ", "").Replace("-", ""), input.RememberMe, false);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    user.AccessFailedCount = 3;
                }
                else if (user.AccessFailedCount == attemptsBefore)
                {
                    // Identity does not count failed recovery-code attempts.
                    user.AccessFailedCount++;
                }
                if (user.AccessFailedCount >= 3) user.RequiresAdministratorUnlock = true;
                UserManagementService.Ensure(await signIn.UserManager.UpdateAsync(user));
            }
            return result.Succeeded ? LoginOutcome.Succeeded : LoginOutcome.Failed;
        }, CancellationToken.None);
    }
    public Task LogoutAsync() => StorageErrors.ExecuteAsync(() => signIn.SignOutAsync());
    public Task<byte[]?> AvatarAsync() => current.UserId is { } id
        ? StorageErrors.ExecuteAsync(() => signIn.UserManager.Users.AsNoTracking().Where(x => x.Id == id).Select(x => x.AvatarUrl).FirstOrDefaultAsync())
        : Task.FromResult<byte[]?>(null);
}
