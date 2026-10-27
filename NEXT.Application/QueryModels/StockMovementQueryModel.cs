using NEXT.Application.Common.QueryModels;
using NEXT.Domain.Enums;

namespace NEXT.Application.QueryModels;

public sealed class StockMovementQueryModel : PaginationQuery
{
    public Guid ProductId { get; set; }

    public Guid? WarehouseId { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }
}
