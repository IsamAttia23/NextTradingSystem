using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.QueryModels;
public sealed class BranchQueryModel : PaginationQuery
{
    public bool? IsActive { get; set; }
}
