using NEXT.Application.Abstractions.Context;
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

        services.AddScoped<NEXT.Application.Abstractions.Services.IPaginationServices, PaginationServices>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRequestMetadataService, RequestMetadataService>();
        services.AddScoped(typeof(IRepository<>), typeof(TRepository<>));
        services.AddScoped(typeof(IKeyRepository<,>), typeof(KeyRepository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        // Register only dedicated repository contracts, without a service locator in UnitOfWork.
        foreach (var implementation in typeof(TRepository<>).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters))
        {
            foreach (var contract in implementation.GetInterfaces().Where(contract =>
                contract.Namespace == "NEXT.Application.Abstractions.Persistence.Repositories"))
                services.AddScoped(contract, implementation);
        }

        services.AddScoped<AuditEntryFactory>();
        services.AddScoped<AuditFieldsHandler>();
        services.AddScoped<AuditValueSerializer>();

        services.AddScoped<SoftDeleteHandler>();
        services.AddScoped<TrackedChangesProcessor>();

        services.AddScoped(typeof(NEXT.Application.Features.RecycleBin.IRecycleBinService<,>), typeof(NEXT.Infrastructure.Features.RecycleBin.RecycleBinService<,>));
        services.AddNEXTIdentity();
        return services;
    }
}
