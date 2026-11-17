using NEXT.Application.Validation;
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

    [NotEmptyGuid]
    public Guid? CategoryId { get; set; }

    [NotEmptyGuid]
    public Guid? UnitOfMeasureId { get; set; }

    [NotEmptyGuid]
    public Guid? PackagingUnitId { get; set; }

    [Range(1, int.MaxValue)]
    public int? UnitsPerPackage { get; set; }
    [ScaffoldColumn(false)]
    public bool AddOpeningStock { get; set; }
    [ScaffoldColumn(false)]
    public Guid? OpeningWarehouseId { get; set; }

    [ScaffoldColumn(false)]
    public bool OpeningInPackages { get; set; }
    [ScaffoldColumn(false)]
    public decimal? OpeningQuantity { get; set; }

    [ScaffoldColumn(false)]
    public string? OpeningBatchNumber { get; set; }

    [ScaffoldColumn(false)]
    public DateOnly? OpeningExpiryDate { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    [Required(ErrorMessage = "ProductPriceRequired")]
    public decimal CostPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    // Markup percentage, for example 25 means cost plus 25%.
    [Display(Name = "RetailPricePercent")]
    [Required(ErrorMessage = "ProductPriceRequired")]
    public decimal RetailPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    // Markup percentage, for example 25 means cost plus 25%.
    [Display(Name = "WholesalePricePercent")]
    [Required(ErrorMessage = "ProductPriceRequired")]
    public decimal WholesalePrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    // Markup percentage, for example 25 means cost plus 25%.
    [Display(Name = "VipPricePercent")]
    [Required(ErrorMessage = "ProductPriceRequired")]
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
