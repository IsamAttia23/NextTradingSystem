using System.ComponentModel.DataAnnotations;
using NEXT.Domain.Enums;

namespace NEXT.Application.Common.QueryModels;

public class PaginationQuery
{
    public virtual IReadOnlyList<string> GetSortFields() => [];

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public SortDirection? SortDirection { get; set; }
    [StringLength(200)]
    public string? SearchTerm { get; set; }
}
