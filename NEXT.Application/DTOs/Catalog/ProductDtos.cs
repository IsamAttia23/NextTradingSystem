using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;
namespace NEXT.Application.DTOs.Catalog;
public class ProductInputDto
{
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [StringLength(100)] public string? Barcode { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
     public Guid? CategoryId { get; set; }
     public Guid? UnitOfMeasureId { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal CostPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal RetailPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal WholesalePrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal VipPrice { get; set; }
    [Range(0,100)] public decimal TaxRate { get; set; }
    [Range(typeof(decimal), "0", "999999999999.999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal MinStock { get; set; }
    [Range(typeof(decimal), "0", "999999999999.999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] public decimal? MaxStock { get; set; }
     public bool HasExpiry { get; set; }
     public bool IsActive { get; set; } = true;
}
public sealed class ProductDto : ProductInputDto
{
    public Guid Id { get; set; }

}
