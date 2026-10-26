using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity;
using NEXT.Infrastructure.Persistence;
using NEXT.Application.Abstractions.Persistence;
using NEXT.Infrastructure.Persistence.Repositories;
using NEXT.Infrastructure.Persistence.SaveChanges;
using NEXT.Infrastructure.Persistence.UsersManagment;
using NEXT.Application.Abstractions.Identity;
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

        services.AddScoped<IUserCommandService, UserCommandService>();
        services.AddScoped<IUserQueryService, UserQueryService>();
        services.AddScoped<IRoleManagementService, RoleManagementService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<NEXTDbContext>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRequestMetadataService, RequestMetadataService>();
        services.AddScoped(typeof(IRepository<>), typeof(TRepository<>));
        services.AddScoped(typeof(IKeyRepository<,>), typeof(KeyRepository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<AuditEntryFactory>();
        services.AddScoped<AuditFieldsHandler>();
        services.AddScoped<AuditValueSerializer>();
        services.AddScoped<SaveChangesStatePreserver>();
        services.AddScoped<SoftDeleteHandler>();
        services.AddScoped<TrackedChangesProcessor>();

        services.AddNEXTIdentity();
        return services;
    }
}
