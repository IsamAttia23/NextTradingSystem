using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.Repositories;
using NEXT.Infrastructure.Services;

namespace NEXT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NEXTDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'NEXTDatabase' was not configured.");

        services.AddDbContext<NEXTDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(30));
        });

        services.AddIdentityCore<ApplicationUser>()
                 .AddRoles<IdentityRole>()
                 .AddEntityFrameworkStores<NEXTDbContext>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRequestMetadataService, RequestMetadataService>();
        services.AddDbContext<NEXTDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(30));
        });
        services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddNEXTIdentity();
        return services;
    }
}
