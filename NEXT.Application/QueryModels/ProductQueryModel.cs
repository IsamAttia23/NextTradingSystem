using NEXT.Application.Common.QueryModels;

using NEXT.Domain.Enums;

namespace NEXT.Application.QueryModels;

public sealed class ProductQueryModel : PaginationQuery
{
    // Product filters
    public string? Barcode { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }

    // Category and unit of measure
    public Guid? CategoryId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }

    // Cost price range
    public decimal? MinCostPrice { get; set; }
    public decimal? MaxCostPrice { get; set; }

    // Retail price range
    public decimal? MinRetailPrice { get; set; }
    public decimal? MaxRetailPrice { get; set; }

    // Wholesale price range
    public decimal? MinWholesalePrice { get; set; }
    public decimal? MaxWholesalePrice { get; set; }

    // VIP price range
    public decimal? MinVipPrice { get; set; }
    public decimal? MaxVipPrice { get; set; }

    // Tax rate range
    public decimal? MinTaxRate { get; set; }
    public decimal? MaxTaxRate { get; set; }

    // Stock range
    public decimal? MinStock { get; set; }
    public decimal? MaxStock { get; set; }

    // Product status
    public bool? IsActive { get; set; }
    public bool? HasExpiry { get; set; }

}
