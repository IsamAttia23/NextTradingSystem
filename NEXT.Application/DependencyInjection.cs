using Mapster;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Services;
using NEXT.Application.Validation;

namespace NEXT.Application;

public static class EntityServiceRegistrationExtensions
{
    /// <summary>Registers generic application services and their default entity validator.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMapster();
        services.TryAddScoped(typeof(IEntityValidator<>), typeof(DataAnnotationsEntityValidator<>));
        services.TryAddScoped(typeof(IEntityServices<,,,>), typeof(TEntityService<,,,,>));
        return services;
    }
}
