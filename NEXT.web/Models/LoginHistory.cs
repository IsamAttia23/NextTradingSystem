using System.Globalization;
using System.Security.Claims;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Models;

internal static class LoginHistory
{
    public static IReadOnlyList<DetailFieldDto> Fields(UserDto? user, ClaimsPrincipal principal) =>
    [
        new("LastSignIn", Format(PreviousLogin(principal))),
        new("LastInvalidSignIn", Format(user?.LastInvalidLoginAttempt))
    ];

    private static DateTime? PreviousLogin(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(AccountClaimTypes.PreviousLogin);
        if (DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
        {
            return timestamp;
        }
        return null;
    }

    private static string Format(DateTime? timestamp)
    {
        if (timestamp is null)
        {
            return "—";
        }
        return timestamp.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }
}
