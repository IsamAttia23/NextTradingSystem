using System.Security.Claims;
using NEXT.Application.DTOs.Authentication;

namespace NEXT.Application.Abstractions.Identity;

public interface IAccountAccess
{
    Task<LoginOutcome> LoginAsync(LoginDto input);
    Task<bool> HasChallengeAsync();
    Task<LoginOutcome> VerifyAsync(VerifyLoginDto input);
    Task<bool> IsSessionValidAsync(ClaimsPrincipal? principal);
    Task LogoutAsync();
    Task<byte[]?> AvatarAsync();
}
