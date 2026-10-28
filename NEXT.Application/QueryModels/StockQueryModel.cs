using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.QueryModels;
public sealed class StockQueryModel : PaginationQuery
{
    public Guid? ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public decimal? MinAvailableQuantity { get; set; }
    public decimal? MaxAvailableQuantity { get; set; }
}
