using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Application.QueryModels;

public sealed class StockMovementQueryModel : PaginationQuery
{
    public StockMovementQueryModel() { SortBy = "CreatedAt"; SortDirection = NEXT.Domain.Enums.SortDirection.Descending; }
    public Guid? ProductId { get; set; }

    public StockMovementType? Type { get; set; }

    public Guid? WarehouseId { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }
}
