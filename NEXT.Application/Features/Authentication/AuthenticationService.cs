using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.Authentication;

/// <summary>Application-level authentication workflow; no MVC or ASP.NET Identity dependency.</summary>
public sealed class AuthenticationService(IAuthenticationGateway gateway, ILoginAccessPolicy access)
{
    public async Task<AuthenticationOutcome> LoginAsync(string userName, string password, bool rememberMe)
    {
        var id = await gateway.FindUserIdAsync(userName);
        if (id is null || !await access.CanSignInAsync(id.Value)) return AuthenticationOutcome.Failed;
        return await gateway.PasswordSignInAsync(userName, password, rememberMe);
    }

    public Task<bool> HasPendingTwoFactorUserAsync() => gateway.HasPendingTwoFactorUserAsync();

    public async Task<AuthenticationOutcome> VerifyAsync(string code, bool recoveryCode, bool rememberMe)
    {
        var id = await gateway.GetPendingTwoFactorUserIdAsync();
        if (id is null || !await access.CanSignInAsync(id.Value)) return AuthenticationOutcome.Failed;
        return await gateway.VerifyTwoFactorAsync(code, recoveryCode, rememberMe);
    }

    public Task LogoutAsync() => gateway.SignOutAsync();
}
