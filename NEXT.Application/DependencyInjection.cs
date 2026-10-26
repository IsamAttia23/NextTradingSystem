using Mapster;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Services;
using NEXT.Application.Validation;
using NEXT.Application.Features.ApplicationUsers.Mapping;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Services.Security;

namespace NEXT.Application;

public static class EntityServiceRegistrationExtensions
{
    /// <summary>Registers application use cases, shared CQRS handlers, Mapster, validation, and entity services.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        TypeAdapterConfig.GlobalSettings.Scan(typeof(ApplicationUserMappingRegister).Assembly);
        services.AddMapster();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateApplicationUserHandler).Assembly));
        services.TryAddScoped(typeof(IEntityValidator<>), typeof(DataAnnotationsEntityValidator<>));
        services.TryAddScoped<IPaginationServices, PaginationServices>();
        services.AddScoped<IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto>, PermissionEntityService>();
        return services;
    }
}
