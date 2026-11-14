using NEXT.Infrastructure.Identity.Entities;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Identity;

public static class IdentityOptionsSetup
{
    public static IdentityBuilder AddNEXTIdentity(this IServiceCollection services)
    {
        services.AddDataProtection();
        services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            // Identity configures its application cookie events during options setup.
            options.Events.OnSigningIn = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity
                    && context.HttpContext.Items.TryGetValue(AccountClaimTypes.PreviousLogin, out var value)
                    && value is DateTime previousLogin)
                {
                    identity.AddClaim(new Claim(AccountClaimTypes.PreviousLogin,
                        previousLogin.ToString("O", CultureInfo.InvariantCulture)));
                }
                return Task.CompletedTask;
            };
        });
        return services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 3;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddRoles<Role>()
        .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
        .AddEntityFrameworkStores<Persistence.NEXTDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();
    }
}
