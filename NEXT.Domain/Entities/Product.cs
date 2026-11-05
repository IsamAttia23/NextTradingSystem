using NEXT.Domain.Common.Entities;
using NEXT.Domain.Enums;

namespace NEXT.Domain.Entities;

public class Product : IsDeletableEntity<Guid>
{
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
    public decimal CostPrice { get; set; }
    // Markup percentage over cost.
    public decimal RetailPrice { get; set; }
    // Markup percentage over cost.
    public decimal WholesalePrice { get; set; }
    // Markup percentage over cost.
    public decimal VipPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal MinStock { get; set; }
    public decimal? MaxStock { get; set; }
    public bool HasExpiry { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];

    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<SalesInvoiceItem> SalesInvoiceItems { get; set; } = new List<SalesInvoiceItem>();
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<ProductBatch> Batches { get; set; } = new List<ProductBatch>();
}
