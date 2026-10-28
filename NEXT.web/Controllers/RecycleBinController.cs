using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.Features.RecycleBin;
using NEXT.web.Models;

namespace NEXT.web.Controllers;

public sealed class RecycleBinController(IEnumerable<IRecycleBinSection> sections) : WorkspaceController
{
    public async Task<IActionResult> Index(string section = "Product", int page = 1)
    {
        var service = Find(section);
        if (service is null) return NotFound();
        ViewData["Sections"] = sections.Select(x => x.Name).ToArray();
        ViewData["Section"] = section;
        var result = await service.List(new PaginationQuery { PageNumber = page, PageSize = 20 });
        return Table(new("RecycleBin", "RecycleDescription", ["RecordId", "DeletedOn"],
            result.Data.Select(x => new TableRow([x.EntityId.ToString(), x.DeletedOn.ToLocalTime().ToString("g")],
                [new("Restore", Route("Restore", new { section, id = x.EntityId })), new("Purge", Route("Purge", new { section, id = x.EntityId }), true)])).ToArray(),
            Page: result.PageNumber, TotalPages: result.TotalPages, TotalCount: result.TotalCount));
    }
    [HttpGet] public IActionResult Restore(string section, Guid id) => Find(section) is null ? NotFound() : Confirm("Restore", "Restore", new { section, id }, "RestoreWarning");
    [HttpPost, ActionName("Restore")] public async Task<IActionResult> RestoreConfirmed(string section, Guid id, CancellationToken ct)
    {
        var service = Find(section);
        if (service is null) return NotFound();
        await service.Restore(id, ct);
        return Ok();
    }
    [HttpGet] public IActionResult Purge(string section, Guid id) => Find(section) is null ? NotFound() : Confirm("Purge", "Purge", new { section, id }, "PurgeWarning");
    [HttpPost, ActionName("Purge")] public async Task<IActionResult> PurgeConfirmed(string section, Guid id, CancellationToken ct)
    {
        var service = Find(section);
        if (service is null) return NotFound();
        await service.Purge(id, ct);
        return Ok();
    }
    private IRecycleBinSection? Find(string section) => sections.SingleOrDefault(x => x.Name == section);
}
