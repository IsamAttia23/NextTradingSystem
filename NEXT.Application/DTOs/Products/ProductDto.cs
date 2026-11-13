using System.ComponentModel.DataAnnotations;
using NEXT.Application.DTOs.Common;

namespace NEXT.Application.DTOs.Products;

public sealed class ProductDto : IVersionedRecordDto
{
    public Guid Id { get; set; }
    public byte[] RowVersion { get; set; } = [];
    [ScaffoldColumn(false)]
    public decimal? ExchangeRate { get; set; }
    public string Name { get; set; } = "";
    public string? Barcode { get; set; }
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal RetailPrice { get; set; }
    public decimal WholesalePrice { get; set; }
    public decimal VipPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal MinStock { get; set; }
    public decimal? MaxStock { get; set; }
    public bool HasExpiry { get; set; }
    public bool IsActive { get; set; } = true;
}
