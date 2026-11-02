using NEXT.Application.Features.RecycleBin;
using NEXT.Application.Features;
using NEXT.Application.Services.Inventory;
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

public static class ApplicationRegistrationExtensions
{
    /// <summary>Registers application use cases, shared CQRS handlers, Mapster, validation, and entity services.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        var mapping = new TypeAdapterConfig();
        mapping.Scan(typeof(ApplicationUserMappingRegister).Assembly);
        services.AddSingleton(mapping);
        services.AddMapster();
        services.AddScoped<NEXT.Application.Abstractions.Identity.IUserDeletionPolicy, NEXT.Application.Features.ApplicationUsers.UserDeletionPolicy>();
        services.AddScoped<IRecycleBinAccess, RecycleBinAccess>();
        services.AddScoped(typeof(IRecycleBinService<,>), typeof(RecycleBinService<,>));
        services.AddScoped<NEXT.Application.Abstractions.Context.IDeletionAuditContext, DeletionAuditContext>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IInventoryQueryService, InventoryQueryService>();
        services.AddEntityFeatures();
        services.AddScoped<NEXT.Application.Features.ApplicationUsers.UserIdentityRules>();
        services.AddScoped<NEXT.Application.Features.ApplicationUsers.UserDtoReader>();
        services.AddScoped<NEXT.Application.Features.ApplicationUsers.UserRoleAssignment>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateApplicationUserHandler).Assembly));
        services.TryAddScoped(typeof(IEntityValidator<>), typeof(DataAnnotationsEntityValidator<>));

        services.AddEntityCqrs<Permission, PermissionDto, PermissionCreationDto, PermissionUpdateDto>();
        services.AddScoped<IEntityRules<Permission, PermissionCreationDto>, PermissionCreationRules>();
        services.AddScoped<IEntityRules<Permission, PermissionUpdateDto>, PermissionUpdateRules>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Product>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Customer>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Supplier>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Category>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Tag>>();
        services.AddScoped<IRecycleBinSection, RecycleBinSection<Warehouse>>();
        return services;
    }

    /// <summary>Registers the same shared handlers for every entity feature.</summary>
    public static IServiceCollection AddEntityCqrs<TEntity, TDto, TCreationDto, TUpdateDto>(this IServiceCollection services)
        where TEntity : AuditableEntity<Guid> where TDto : class where TCreationDto : class where TUpdateDto : class
    {
        var queryContract = typeof(IRequestHandler<GetEntitiesQuery<TDto>, TPaginationQueryModel<TDto>>);
        var existing = services.FirstOrDefault(x => x.ServiceType == queryContract);
        if (existing is not null && existing.ImplementationType != typeof(GetEntitiesQueryHandler<TEntity, TDto>))
            throw new InvalidOperationException($"DTO '{typeof(TDto).Name}' already belongs to another entity registration.");
        services.TryAddScoped<IRequestHandler<CreateEntityCommand<TDto, TCreationDto>, TDto>, CreateEntityCommandHandler<TEntity, TDto, TCreationDto>>();
        services.TryAddScoped<IRequestHandler<UpdateEntityCommand<TDto, TUpdateDto>, TDto>, UpdateEntityCommandHandler<TEntity, TDto, TUpdateDto>>();
        services.TryAddScoped<IRequestHandler<DeleteEntityCommand<TDto>>, DeleteEntityCommandHandler<TEntity, TDto>>();
        services.TryAddScoped<IRequestHandler<GetEntityByIdQuery<TDto>, TDto>, GetEntityByIdQueryHandler<TEntity, TDto>>();
        services.TryAddScoped<IRequestHandler<GetEntitiesQuery<TDto>, TPaginationQueryModel<TDto>>, GetEntitiesQueryHandler<TEntity, TDto>>();
        services.TryAddScoped<IRequestHandler<GetEntityActivityQuery<TDto>, NEXT.Application.DTOs.Presentation.ActivityPageDto>, GetEntityActivityQueryHandler<TEntity, TDto>>();
        return services;
    }
}
