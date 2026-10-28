using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Catalog;
using NEXT.Domain.Entities;
namespace NEXT.Application.Services.Catalog;
public static class CatalogRegistration
{
    public static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        services.AddScoped<ICatalogService<CustomerDto, CustomerInputDto>, CatalogService<Customer, CustomerDto, CustomerInputDto>>();
        services.AddScoped<ICatalogRules<Customer, CustomerInputDto>, CustomerRules>();
        services.AddScoped<ICatalogService<CustomerContactDto, CustomerContactInputDto>, CatalogService<CustomerContact, CustomerContactDto, CustomerContactInputDto>>();
        services.AddScoped<ICatalogRules<CustomerContact, CustomerContactInputDto>, CustomerContactRules>();
        services.AddScoped<ICatalogService<TagDto, TagInputDto>, CatalogService<Tag, TagDto, TagInputDto>>();
        services.AddScoped<ICatalogService<SupplierDto, SupplierInputDto>, CatalogService<Supplier, SupplierDto, SupplierInputDto>>();
        services.AddScoped<ICatalogRules<Supplier, SupplierInputDto>, SupplierRules>();
        services.AddScoped<ICatalogService<CategoryDto, CategoryInputDto>, CatalogService<Category, CategoryDto, CategoryInputDto>>();
        services.AddScoped<ICatalogRules<Category, CategoryInputDto>, CategoryRules>();
        services.AddScoped<ICatalogService<BranchDto, BranchInputDto>, CatalogService<Branch, BranchDto, BranchInputDto>>();
        services.AddScoped<ICatalogService<WarehouseDto, WarehouseInputDto>, CatalogService<Warehouse, WarehouseDto, WarehouseInputDto>>();
        services.AddScoped<ICatalogRules<Warehouse, WarehouseInputDto>, WarehouseRules>();
        services.AddScoped<ICatalogService<ProductDto, ProductInputDto>, CatalogService<Product, ProductDto, ProductInputDto>>();
        services.AddScoped<ICatalogRules<Product, ProductInputDto>, ProductRules>();
        services.AddScoped<ICatalogService<UnitOfMeasureDto, UnitOfMeasureInputDto>, CatalogService<UnitOfMeasure, UnitOfMeasureDto, UnitOfMeasureInputDto>>();
        services.AddScoped<ICatalogRules<UnitOfMeasure, UnitOfMeasureInputDto>, UnitOfMeasureRules>();
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
