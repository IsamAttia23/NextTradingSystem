using Microsoft.AspNetCore.Identity;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Identity;

/// <summary>Executes ASP.NET Core Identity operations; business workflow stays in Application.</summary>
public sealed class IdentityAuthenticationGateway(SignInManager<ApplicationUser> signIn) : IAuthenticationGateway
{
    public async Task<Guid?> FindUserIdAsync(string userName) =>
        (await signIn.UserManager.FindByNameAsync(userName))?.Id;

    public async Task<AuthenticationOutcome> PasswordSignInAsync(string userName, string password, bool rememberMe)
    {
        var user = await signIn.UserManager.FindByNameAsync(userName);
        if (user is null) return AuthenticationOutcome.Failed;
        var result = await signIn.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
        return Map(result);
    }

    public async Task<bool> HasPendingTwoFactorUserAsync() =>
        await signIn.GetTwoFactorAuthenticationUserAsync() is not null;

    public async Task<Guid?> GetPendingTwoFactorUserIdAsync() =>
        (await signIn.GetTwoFactorAuthenticationUserAsync())?.Id;

    public async Task<AuthenticationOutcome> VerifyTwoFactorAsync(string code, bool recoveryCode, bool rememberMe)
    {
        var result = recoveryCode
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(code.Replace(" ", ""))
            : await signIn.TwoFactorAuthenticatorSignInAsync(code.Replace(" ", "").Replace("-", ""), rememberMe, false);
        return Map(result);
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    private static AuthenticationOutcome Map(SignInResult result) =>
        result.Succeeded ? AuthenticationOutcome.Succeeded :
        result.RequiresTwoFactor ? AuthenticationOutcome.RequiresTwoFactor :
        AuthenticationOutcome.Failed;
}
