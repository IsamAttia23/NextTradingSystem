using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.QueryModels;
public sealed class UnitOfMeasureQueryModel : PaginationQuery
{
    public Guid? BaseUnitId { get; set; }
}
