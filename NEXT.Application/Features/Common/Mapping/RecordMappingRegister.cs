using NEXT.Application.DTOs.Suppliers;
using NEXT.Application.DTOs.Customers;
using Mapster;

using NEXT.Domain.Entities;
namespace NEXT.Application.Features.Common.Mapping;
public sealed class RecordMappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Customer, CustomerDto>().Map(d => d.TagName, s => s.Tag.Name).Map(d => d.TagColor, s => s.Tag.Color);
        config.NewConfig<Supplier, SupplierDto>().Map(d => d.TagName, s => s.Tag.Name).Map(d => d.TagColor, s => s.Tag.Color);
    }
}
