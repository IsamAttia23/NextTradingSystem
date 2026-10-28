using NEXT.Application.DTOs.Authentication;
namespace NEXT.Application.Abstractions.Identity;
public enum LoginOutcome { Failed, Succeeded, RequiresTwoFactor, MissingChallenge }
public interface IAccountAccess
{
    Task<LoginOutcome> LoginAsync(LoginDto input);
    Task<bool> HasChallengeAsync();
    Task<LoginOutcome> VerifyAsync(VerifyLoginDto input);
    Task LogoutAsync();
    Task<byte[]?> AvatarAsync();
}
