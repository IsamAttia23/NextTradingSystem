using System.ComponentModel.DataAnnotations;

namespace NEXT.Application.DTOs.Products;

public class ProductInputDto
{
    [ScaffoldColumn(false)]
    [Range(typeof(decimal), "0.00000001", "1000000000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? ExchangeRate { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = "";
    [StringLength(100)]
    public string? Barcode { get; set; }
    [StringLength(1000)]
    public string? Description { get; set; }
    [NEXT.Application.Validation.NotEmptyGuid]
    public Guid? CategoryId { get; set; }
    [NEXT.Application.Validation.NotEmptyGuid]
    public Guid? UnitOfMeasureId { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal CostPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    // Markup percentage, for example 25 means cost plus 25%.
    [Display(Name = "RetailPricePercent")]
    public decimal RetailPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    // Markup percentage, for example 25 means cost plus 25%.
    [Display(Name = "WholesalePricePercent")]
    public decimal WholesalePrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    // Markup percentage, for example 25 means cost plus 25%.
    [Display(Name = "VipPricePercent")]
    public decimal VipPrice { get; set; }
    [Range(0, 100)]
    public decimal TaxRate { get; set; }
    [Range(typeof(decimal), "0", "999999999999.999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal MinStock { get; set; }
    [Range(typeof(decimal), "0", "999999999999.999", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? MaxStock { get; set; }
    public bool HasExpiry { get; set; }
    public bool IsActive { get; set; } = true;
}
