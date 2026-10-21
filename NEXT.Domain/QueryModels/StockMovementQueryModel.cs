using NEXT.Domain.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Domain.QueryModels;

public sealed class StockMovementQueryModel : PaginationQuery
{
    public Guid ProductId { get; set; }

    public Guid? WarehouseId { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }
}