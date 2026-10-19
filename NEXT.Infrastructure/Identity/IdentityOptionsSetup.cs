using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NEXT.Domain.Entities;

namespace NEXT.Infrastructure.Identity;

public static class IdentityOptionsSetup
{
    public static IdentityBuilder AddNEXTIdentity(this IServiceCollection services)
    {
        return services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddRoles<Role>()
        .AddEntityFrameworkStores<Persistence.NEXTDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();
    }
}
