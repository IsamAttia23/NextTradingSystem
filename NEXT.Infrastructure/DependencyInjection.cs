using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Identity;
using NEXT.Infrastructure.Persistence;
using NEXT.Infrastructure.Persistence.IRepositories;
using NEXT.Infrastructure.Persistence.Repositories;
using NEXT.Infrastructure.Persistence.SaveChanges;
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
                 .AddRoles<Role>()
                 .AddEntityFrameworkStores<NEXTDbContext>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRequestMetadataService, RequestMetadataService>();
        services.AddDbContext<NEXTDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(30));
        });
        services.AddScoped(typeof(IRepository<>), typeof(TRepository<>));
        services.AddScoped(typeof(IKeyRepository<,>), typeof(KeyRepository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<AuditEntryFactory>();
        services.AddScoped<AuditFieldsHandler>();
        services.AddScoped<AuditValueSerializer>();
        services.AddScoped<SaveChangesStatePreserver>();
        services.AddScoped<SoftDeleteHandler>();
        services.AddScoped<TrackedChangesProcessor>();

        var serializerRegistration = services
    .Any(service => service.ServiceType == typeof(AuditValueSerializer));

        Console.WriteLine(
            $"AuditValueSerializer registered: {serializerRegistration}");


        services.AddNEXTIdentity();
        return services;
    }
}
