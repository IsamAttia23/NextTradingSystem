using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Identity;

public sealed class LoginAccessPolicy(UserManager<ApplicationUser> users, IHostEnvironment environment,
    IConfiguration configuration, IHttpContextAccessor context) : ILoginAccessPolicy
{
    public const string TemporaryClaim = "next:temporary-administrator";

    public async Task<bool> CanSignInAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || user.DeletedAt is not null) return false;
        if (!(await users.GetClaimsAsync(user)).Any(x => x.Type == TemporaryClaim)) return true;
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DevelopmentAccess:Enabled")) return false;
        if (context.HttpContext?.Connection.RemoteIpAddress is { } address && !IPAddress.IsLoopback(address)) return false;
        var administrators = await users.GetUsersInRoleAsync("Admin");
        return !administrators.Any(x => x.Id != user.Id && x.IsActive && x.DeletedAt is null);
    }
}
