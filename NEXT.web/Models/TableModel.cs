using NEXT.Application.DTOs.Presentation;
namespace NEXT.web.Models;

public sealed record TableModel(string Title, string Description, IReadOnlyList<string> Columns,
    IReadOnlyList<TableRow> Rows, UiAction? Create = null, int Page = 1, int TotalPages = 1, int TotalCount = 0,
    NEXT.Application.Common.QueryModels.PaginationQuery? Query = null,
    IReadOnlyList<FieldOption>? SortOptions = null, IReadOnlyList<FormField>? FilterFields = null, int ActiveFilters = 0, int PageSize = 20);
