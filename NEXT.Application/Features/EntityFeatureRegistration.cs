using NEXT.Application.Features.Warehouses;
using NEXT.Application.Features.UnitsOfMeasure;
using NEXT.Application.Features.Suppliers;
using NEXT.Application.Features.Products;
using NEXT.Application.Features.Customers;
using NEXT.Application.Features.CustomerContacts;
using NEXT.Application.Features.Categories;
using NEXT.Application.DTOs.Warehouses;
using NEXT.Application.DTOs.UnitsOfMeasure;
using NEXT.Application.DTOs.Tags;
using NEXT.Application.DTOs.Suppliers;
using NEXT.Application.DTOs.Products;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.CustomerContacts;
using NEXT.Application.DTOs.Common;
using NEXT.Application.DTOs.Categories;
using NEXT.Application.DTOs.Branches;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Services.ReferenceData;

using NEXT.Domain.Entities;
using NEXT.Application.Features.Common.CQRS;
namespace NEXT.Application.Features;

public static class EntityFeatureRegistration
{
    public static IServiceCollection AddEntityFeatures(this IServiceCollection services)
    {
        services.AddEntityCqrs<Customer, CustomerDto, CustomerInputDto, CustomerInputDto>();
        services.AddScoped<IEntityRules<Customer, CustomerInputDto>, CustomerRules>();
        services.AddEntityCqrs<CustomerContact, CustomerContactDto, CustomerContactInputDto, CustomerContactInputDto>();
        services.AddScoped<IEntityRules<CustomerContact, CustomerContactInputDto>, CustomerContactRules>();
        services.AddEntityCqrs<Tag, TagDto, TagInputDto, TagInputDto>();
        services.AddEntityCqrs<Supplier, SupplierDto, SupplierInputDto, SupplierInputDto>();
        services.AddScoped<IEntityRules<Supplier, SupplierInputDto>, SupplierRules>();
        services.AddEntityCqrs<Category, CategoryDto, CategoryInputDto, CategoryInputDto>();
        services.AddScoped<IEntityRules<Category, CategoryInputDto>, CategoryRules>();
        services.AddEntityCqrs<Branch, BranchDto, BranchInputDto, BranchInputDto>();
        services.AddEntityCqrs<Warehouse, WarehouseDto, WarehouseInputDto, WarehouseInputDto>();
        services.AddScoped<IEntityRules<Warehouse, WarehouseInputDto>, WarehouseRules>();
        services.AddEntityCqrs<Product, ProductDto, ProductInputDto, ProductInputDto>();
        services.AddScoped<IEntityRules<Product, ProductInputDto>, ProductRules>();
        services.AddEntityCqrs<UnitOfMeasure, UnitOfMeasureDto, UnitOfMeasureInputDto, UnitOfMeasureInputDto>();
        services.AddScoped<IEntityRules<UnitOfMeasure, UnitOfMeasureInputDto>, UnitOfMeasureRules>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IReferenceSource>(sp => new ReferenceSource<Customer>("Customers",
            sp.GetRequiredService<NEXT.Application.Abstractions.Persistence.IRepository<Customer>>(),
            sp.GetRequiredService<NEXT.Application.Abstractions.Services.IPaginationServices>(), x => new ReferenceOptionDto(x.Id.ToString(), x.Name)));
        services.AddScoped<IReferenceSource>(sp => new ReferenceSource<Tag>("Tags",
            sp.GetRequiredService<NEXT.Application.Abstractions.Persistence.IRepository<Tag>>(),
            sp.GetRequiredService<NEXT.Application.Abstractions.Services.IPaginationServices>(), x => new ReferenceOptionDto(x.Id.ToString(), x.Name)));
        services.AddScoped<IReferenceSource>(sp => new ReferenceSource<Category>("Categories",
            sp.GetRequiredService<NEXT.Application.Abstractions.Persistence.IRepository<Category>>(),
            sp.GetRequiredService<NEXT.Application.Abstractions.Services.IPaginationServices>(), x => new ReferenceOptionDto(x.Id.ToString(), x.Name)));
        services.AddScoped<IReferenceSource>(sp => new ReferenceSource<Branch>("Branches",
            sp.GetRequiredService<NEXT.Application.Abstractions.Persistence.IRepository<Branch>>(),
            sp.GetRequiredService<NEXT.Application.Abstractions.Services.IPaginationServices>(), x => new ReferenceOptionDto(x.Id.ToString(), x.Name)));
        services.AddScoped<IReferenceSource>(sp => new ReferenceSource<Warehouse>("Warehouses",
            sp.GetRequiredService<NEXT.Application.Abstractions.Persistence.IRepository<Warehouse>>(),
            sp.GetRequiredService<NEXT.Application.Abstractions.Services.IPaginationServices>(), x => new ReferenceOptionDto(x.Id.ToString(), x.Name)));
        services.AddScoped<IReferenceSource>(sp => new ReferenceSource<Product>("Products",
            sp.GetRequiredService<NEXT.Application.Abstractions.Persistence.IRepository<Product>>(),
            sp.GetRequiredService<NEXT.Application.Abstractions.Services.IPaginationServices>(), x => new ReferenceOptionDto(x.Id.ToString(), x.Name)));
        services.AddScoped<IReferenceSource>(sp => new ReferenceSource<UnitOfMeasure>("Units",
            sp.GetRequiredService<NEXT.Application.Abstractions.Persistence.IRepository<UnitOfMeasure>>(),
            sp.GetRequiredService<NEXT.Application.Abstractions.Services.IPaginationServices>(), x => new ReferenceOptionDto(x.Id.ToString(), x.Name)));
        return services;
    }
}
