using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.QueryModels;
public sealed class WarehouseQueryModel : PaginationQuery
{
    public Guid? BranchId { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsDefault { get; set; }
}
