using Microsoft.AspNetCore.Identity;
using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Authentication;
using NEXT.Application.Validation;
using NEXT.Domain.Entities;
namespace NEXT.Infrastructure.Identity;
public sealed class AccountAccess(SignInManager<ApplicationUser> signIn, ILoginAccessPolicy policy,
    ICurrentUserService current, IEntityValidator<LoginDto> loginValidator,
    IEntityValidator<VerifyLoginDto> verifyValidator) : IAccountAccess
{
    public async Task<LoginOutcome> LoginAsync(LoginDto input)
    {
        loginValidator.Validate(input);
        var user = await signIn.UserManager.FindByNameAsync(input.UserName);
        if (user is null || !await policy.CanSignInAsync(user.Id)) return LoginOutcome.Failed;
        var result = await signIn.PasswordSignInAsync(user, input.Password, input.RememberMe, true);
        return result.Succeeded ? LoginOutcome.Succeeded : result.RequiresTwoFactor ? LoginOutcome.RequiresTwoFactor : LoginOutcome.Failed;
    }
    public async Task<bool> HasChallengeAsync() => await signIn.GetTwoFactorAuthenticationUserAsync() is not null;
    public async Task<LoginOutcome> VerifyAsync(VerifyLoginDto input)
    {
        verifyValidator.Validate(input);
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null || !await policy.CanSignInAsync(user.Id)) return LoginOutcome.MissingChallenge;
        var result = input.UseRecoveryCode
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(input.Code.Replace(" ", ""))
            : await signIn.TwoFactorAuthenticatorSignInAsync(input.Code.Replace(" ", "").Replace("-", ""), input.RememberMe, false);
        return result.Succeeded ? LoginOutcome.Succeeded : LoginOutcome.Failed;
    }
    public Task LogoutAsync() => signIn.SignOutAsync();
    public async Task<byte[]?> AvatarAsync() => current.UserId is { } id
        ? (await signIn.UserManager.FindByIdAsync(id.ToString()))?.AvatarUrl : null;
}
