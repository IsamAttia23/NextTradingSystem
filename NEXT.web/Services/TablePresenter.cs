using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Services;

public sealed class TablePresenter(QueryFilterPresenter filters)
{
    public async Task<TableModel> BuildAsync<T>(string title, IReadOnlyList<string> columns,
        TPaginationQueryModel<T> pagedList, PaginationQuery query, Func<T, TableRow> row,
        IReadOnlyList<FieldOption> sortOptions, CancellationToken ct, UiAction? create = null) where T : class
    {
        query.PageNumber = pagedList.PageNumber;
        query.PageSize = pagedList.PageSize;
        return new(title, title + "Description", columns, pagedList.Data.Select(row).ToArray(), create,
            pagedList.PageNumber, pagedList.TotalPages, pagedList.TotalCount, query, sortOptions,
            await filters.FieldsAsync(query, ct), QueryFilterPresenter.ActiveCount(query), pagedList.PageSize);
    }
}
