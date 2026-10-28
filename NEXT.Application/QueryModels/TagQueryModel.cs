using NEXT.Application.Common.QueryModels;
namespace NEXT.Application.QueryModels;
public sealed class TagQueryModel : PaginationQuery
{
    public string? Color { get; set; }
}
