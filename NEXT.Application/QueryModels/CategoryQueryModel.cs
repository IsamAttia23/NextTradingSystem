using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.QueryModels;
public sealed class CategoryQueryModel : PaginationQuery
{
    public Guid? ParentId { get; set; }
    public bool? IsActive { get; set; }
}
