using NEXT.Infrastructure.Identity.Security.Services;
using NEXT.Infrastructure.Identity.Security.Email;
using NEXT.Infrastructure.Identity.Security.Storage;
using NEXT.Infrastructure.Identity.Security.Cryptography;
using NEXT.Infrastructure.Identity.Security.Contracts;
using NEXT.Infrastructure.Identity.Security.Options;
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
using NEXT.Infrastructure.Persistence.UsersManagement;
using NEXT.Application.Abstractions.Identity;
using NEXT.Infrastructure.Services;

namespace NEXT.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<Mapster.IRegister, IdentityMappingRegister>();
        services.AddScoped<NEXT.Application.Features.Dashboard.IDashboardAccountCounts, DashboardAccountCounts>();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        var connectionString = configuration.GetConnectionString("NEXTDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'NEXTDatabase' was not configured.");
        }
        services.AddDbContext<NEXTDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(30));
        });
        services.AddSingleton<NEXT.Application.Abstractions.Media.IProfileImageProcessor, NEXT.Infrastructure.Media.ProfileImageProcessor>();
        services.AddScoped<ILoginAccessPolicy, LoginAccessPolicy>();
        services.AddScoped<IAccountAccess, AccountAccess>();
        services.AddOptions<AccountSecurityOptions>().Bind(configuration.GetSection("AccountSecurity"))
            .Validate(x => x.OtpLifetimeMinutes is >= 1 and <= 10 && x.GrantLifetimeMinutes is >= 1 and <= 10 &&
                x.ActivationOtpLifetimeMinutes is >= 1 and <= 1440, "Invalid account security lifetimes.")
            .Validate(x => SecuritySecrets.IsValidKey(x.SecretKey),
                "Configure AccountSecurity:SecretKey with at least 32 random bytes encoded as base64.")
            .ValidateOnStart();
        services.Configure<SmtpEmailOptions>(configuration.GetSection("Email:Smtp"));
        services.Configure<ResendEmailOptions>(configuration.GetSection("Email:Resend"));
        services.AddOptions<GmailEmailOptions>().Bind(configuration.GetSection("Email:Gmail"))
            .Validate(x => !x.IsEnabled || x.IsConfigured && System.Net.Mail.MailAddress.TryCreate(x.From, out _),
                "Configure Email:Gmail with ClientId, ClientSecret, RefreshToken and a valid From address.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SecuritySecrets>();
        services.AddScoped<SecurityTransactions>();
        services.AddScoped<SecurityRequestLimiter>();
        services.AddScoped<SecurityGrantService>();
        services.AddScoped<IOtpService, EmailOtpService>();
        services.AddScoped<IEmailService, SecurityEmailService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<SmtpEmailTransport>();
        services.AddHttpClient<ResendEmailTransport>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient(GmailAccessTokenProvider.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<GmailAccessTokenProvider>();
        services.AddHttpClient<GmailEmailTransport>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<IEmailTransport, ConfiguredEmailTransport>();
        services.AddScoped<SecurityEmailDispatcher>();
        services.AddScoped<SecurityMaintenance>();
        services.AddHostedService<SecurityEmailWorker>();
        services.AddScoped<IAdministratorLookup, AdministratorLookup>();
        services.AddScoped<NEXT.Application.Features.RecycleBin.IRecycleBinSection, NEXT.Infrastructure.Features.RecycleBin.UserRecycleBinSection>();
        services.AddScoped<TemporaryAdministratorBootstrapper>();
        services.AddScoped<IApplicationInitializer>(sp => sp.GetRequiredService<TemporaryAdministratorBootstrapper>());
        services.AddScoped<IUserCommandService, UserCommandService>();
        services.AddScoped<IUserQueryService, UserQueryService>();
        services.AddScoped<NEXT.Application.Features.ApplicationUsers.Queries.IBranchLookup, BranchLookup>();
        services.AddScoped<IRoleManagementService, RoleManagementService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<NEXT.Application.Abstractions.Services.IPaginationServices, PaginationServices>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IRequestMetadataService, RequestMetadataService>();
        services.AddScoped(typeof(IReadRepository<>), typeof(TRepository<>));
        services.AddScoped(typeof(IRepository<>), typeof(TRepository<>));
        services.AddScoped(typeof(IKeyRepository<,>), typeof(KeyRepository<,>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IQueryExecutor, QueryExecutor>();
        services.AddScoped<IRelatedRecordsReader, RelatedRecordsReader>();
        // Register only dedicated repository contracts, without a service locator in UnitOfWork.
        foreach (var implementation in typeof(TRepository<>).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters))
        {
            foreach (var contract in implementation.GetInterfaces().Where(contract =>
                contract.Namespace == "NEXT.Application.Abstractions.Persistence.Repositories"))
            {
                services.AddScoped(contract, implementation);
                foreach (var generic in contract.GetInterfaces().Where(x => x.IsGenericType &&
                    (x.GetGenericTypeDefinition() == typeof(IReadRepository<>) || x.GetGenericTypeDefinition() == typeof(IRepository<>) ||
                     x.GetGenericTypeDefinition() == typeof(IKeyRepository<,>))))
                {
                    services.AddScoped(generic, sp => sp.GetRequiredService(contract));
                }
            }
        }
        services.AddScoped<AuditEntryFactory>();
        services.AddScoped<AuditFieldsHandler>();
        services.AddScoped<AuditValueSerializer>();
        services.AddScoped<SoftDeleteHandler>();
        services.AddScoped<TrackedChangesProcessor>();
        services.AddScoped(typeof(IRecycleBinStore<,>), typeof(NEXT.Infrastructure.Features.RecycleBin.RecycleBinStore<,>));
        services.AddNEXTIdentity();
        return services;
    }
}
