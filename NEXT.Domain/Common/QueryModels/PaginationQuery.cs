using NEXT.Domain.Enums;

namespace NEXT.Domain.Common.QueryModels;

public class PaginationQuery
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public SortDirection? SortDirection { get; set; }
    public string? SearchTerm { get; set; }
}
