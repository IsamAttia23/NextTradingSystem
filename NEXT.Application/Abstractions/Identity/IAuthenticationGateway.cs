namespace NEXT.Application.Abstractions.Identity;

public enum AuthenticationOutcome { Failed, Succeeded, RequiresTwoFactor }

public interface IAuthenticationGateway
{
    Task<Guid?> FindUserIdAsync(string userName);
    Task<AuthenticationOutcome> PasswordSignInAsync(string userName, string password, bool rememberMe);
    Task<bool> HasPendingTwoFactorUserAsync();
    Task<Guid?> GetPendingTwoFactorUserIdAsync();
    Task<AuthenticationOutcome> VerifyTwoFactorAsync(string code, bool recoveryCode, bool rememberMe);
    Task SignOutAsync();
}
