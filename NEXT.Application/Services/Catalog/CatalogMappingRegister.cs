using Mapster;
using NEXT.Application.DTOs.Catalog;
using NEXT.Domain.Entities;
namespace NEXT.Application.Services.Catalog;
public sealed class CatalogMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Customer, CustomerDto>().Map(d => d.TagName, s => s.Tag.Name).Map(d => d.TagColor, s => s.Tag.Color);
        config.NewConfig<Supplier, SupplierDto>().Map(d => d.TagName, s => s.Tag.Name).Map(d => d.TagColor, s => s.Tag.Color);
    }
}
