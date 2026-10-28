using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Features.RecycleBin;
namespace NEXT.web.Services;
public sealed class RecycleBinPresenter(IRecycleBinAccess access)
{
    public async Task<TableModel> TableAsync(string section, int page, Func<string, object?, string> route)
    {
        var result = await access.ListAsync(section, new PaginationQuery { PageNumber = page, PageSize = 20 });
        return new("RecycleBin", "RecycleDescription", ["RecordId", "DeletedOn", "DeletionReason"],
            result.Data.Select(x => new TableRow([x.EntityId.ToString(), x.DeletedOn.ToLocalTime().ToString("g"), x.DeletionReason ?? "—"],
                [new("Restore", route("Restore", new { section, id = x.EntityId })), new("Purge", route("Purge", new { section, id = x.EntityId }), true)])).ToArray(),
            Page: result.PageNumber, TotalPages: result.TotalPages, TotalCount: result.TotalCount);
    }
}
