using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity.Entities;
using NEXT.Application.Abstractions.Context;

namespace NEXT.Infrastructure.Stores;

public sealed class StoreClaimsPrincipalFactory(UserManager<ApplicationUser> users, RoleManager<Role> roles,
    IOptions<IdentityOptions> options, ICurrentStore store) : UserClaimsPrincipalFactory<ApplicationUser, Role>(users, roles, options)
{
    public const string StoreClaim = "next:store";
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        foreach (var old in identity.FindAll(StoreClaim).ToArray()) identity.RemoveClaim(old);
        identity.AddClaim(new Claim(StoreClaim, store.Key));
        return identity;
    }

    public static bool Matches(ClaimsPrincipal? principal, ICurrentStore store)
    {
        var claims = principal?.FindAll(StoreClaim).ToArray() ?? [];
        return claims.Length == 0 ? store.IsDefault : claims.Length == 1 && claims[0].Value == store.Key;
    }
}
