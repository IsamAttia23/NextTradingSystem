using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.QueryModels;
public sealed class ProductBatchQueryModel : PaginationQuery
{
    public ProductBatchQueryModel() { SortBy = "ExpiryDate"; }
    public Guid? ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
