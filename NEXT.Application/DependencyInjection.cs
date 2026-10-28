using NEXT.Application.Features.RecycleBin;
using NEXT.Domain.Entities;
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
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Common.Entities;

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

        services.AddEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto, PermissionEntityService>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Product>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Customer>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Supplier>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Category>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Tag>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Warehouse>>();
        return services;
    }

    /// <summary>Registers a service and its five closed generic request handlers together.</summary>
    public static IServiceCollection AddEntityServices<TId, TDto, TCreationDto, TUpdateDto, TService>(
        this IServiceCollection services)
        where TId : struct
        where TDto : class, TIdType<TId>
        where TCreationDto : class
        where TUpdateDto : class, TIdType<TId>
        where TService : class, IEntityServices<TId, TDto, TCreationDto, TUpdateDto>
    {
        services.TryAddScoped<IEntityServices<TId, TDto, TCreationDto, TUpdateDto>, TService>();
        // Assembly scanning does not close these four-parameter generic handler types.
        services.TryAddScoped<IRequestHandler<CreateEntityCommand<TId, TDto, TCreationDto, TUpdateDto>, TDto>,
            CreateEntityCommandHandler<TId, TDto, TCreationDto, TUpdateDto>>();
        services.TryAddScoped<IRequestHandler<UpdateEntityCommand<TId, TDto, TCreationDto, TUpdateDto>, TDto>,
            UpdateEntityCommandHandler<TId, TDto, TCreationDto, TUpdateDto>>();
        services.TryAddScoped<IRequestHandler<DeleteEntityCommand<TId, TDto, TCreationDto, TUpdateDto>>,
            DeleteEntityCommandHandler<TId, TDto, TCreationDto, TUpdateDto>>();
        services.TryAddScoped<IRequestHandler<GetEntityByIdQuery<TId, TDto, TCreationDto, TUpdateDto>, TDto?>,
            GetEntityByIdQueryHandler<TId, TDto, TCreationDto, TUpdateDto>>();
        services.TryAddScoped<IRequestHandler<GetEntitiesQuery<TId, TDto, TCreationDto, TUpdateDto>, TPaginationQueryModel<TDto>>,
            GetEntitiesQueryHandler<TId, TDto, TCreationDto, TUpdateDto>>();
        return services;
    }
}
