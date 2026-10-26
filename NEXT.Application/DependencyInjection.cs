using Mapster;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.Services;
using NEXT.Application.Validation;

namespace NEXT.Application;

public static class EntityServiceRegistrationExtensions
{
    /// <summary>Registers generic application services, CQRS handlers, and the default entity validator.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMapster();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateApplicationUserHandler).Assembly));
        services.TryAddScoped(typeof(IEntityValidator<>), typeof(DataAnnotationsEntityValidator<>));
        services.TryAddScoped<IPaginationServices, PaginationServices>();
        return services;
    }
}
