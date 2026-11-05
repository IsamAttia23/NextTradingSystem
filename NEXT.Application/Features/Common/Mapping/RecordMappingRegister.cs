using NEXT.Application.DTOs.Suppliers;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.Inventory;
using NEXT.Application.DTOs.Common;
using NEXT.Application.Features.RecycleBin;
using NEXT.Application.Features.ApplicationUsers.Queries;
using Mapster;

using NEXT.Domain.Entities;
using NEXT.Domain.Common.Entities;
using System.Linq.Expressions;

namespace NEXT.Application.Features.Common.Mapping;

public sealed class RecordMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Customer, CustomerDto>().Map(d => d.TagName, s => s.Tag.Name).Map(d => d.TagColor, s => s.Tag.Color);
        config.NewConfig<Supplier, SupplierDto>().Map(d => d.TagName, s => s.Tag.Name).Map(d => d.TagColor, s => s.Tag.Color);
        ConfigureStockMapping(config, stock => stock.Product.CostPrice);
        config.NewConfig<StockMovement, StockMovementDto>()
            .Map(d => d.Type, s => s.Type.ToString());
        config.NewConfig<ProductBatch, ProductBatchDto>();
        config.NewConfig<Branch, BranchOption>();
        foreach (var type in new[] { typeof(Customer), typeof(Tag), typeof(Category), typeof(Branch),
                     typeof(Warehouse), typeof(Product), typeof(UnitOfMeasure) })
        {
            config.ForType(type, typeof(ReferenceOptionDto)).Map("Value", "Id").Map("Label", "Name");
        }
        config.NewConfig<Permission, ReferenceOptionDto>()
                    .Map(d => d.Value, s => s.Id.ToString())
                    .Map(d => d.Label, s => s.Module + " / " + s.Code);
        config.When((source, destination, _) => typeof(IIsDeletableEntity).IsAssignableFrom(source) &&
                destination.IsGenericType && destination.GetGenericTypeDefinition() == typeof(RecycleBinItemDto<>))
            .Map("EntityId", "Id").Map("DeletedOn", "DeletedAt").Ignore("EntityType");
    }

    public static void ConfigureStockMapping(TypeAdapterConfig config, Expression<Func<Stock, decimal>> costPrice)
    {
        config.NewConfig<Stock, StockDto>()
            .Map(d => d.ProductName, s => s.Product.Name)
            .Map(d => d.WarehouseName, s => s.Warehouse.Name)
            .Map(d => d.AvailableQuantity, s => s.Quantity - s.ReservedQuantity)
            .Map(d => d.CostPrice, costPrice);
    }
}
