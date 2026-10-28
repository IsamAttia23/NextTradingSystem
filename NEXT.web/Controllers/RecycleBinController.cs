using NEXT.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Features.RecycleBin;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public sealed class RecycleBinController(IRecycleBinAccess access, RecycleBinPresenter presenter) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Index(string section = "Product", int page = 1)
    {
        ViewData["Sections"] = access.Sections.ToArray();
        ViewData["Section"] = section;
        return Table(await presenter.TableAsync(section, page, (action, values) => Route(action, values)));
    }
    [HttpGet] public IActionResult Restore(string section, Guid id)
    {
        access.ValidateSection(section);
        return Confirm("Restore", "Restore", new { section, id }, "RestoreWarning");
    }
    [HttpPost, ActionName("Restore")] public async Task<IActionResult> RestoreConfirmed(string section, Guid id, CancellationToken ct)
    {
        await access.RestoreAsync(section, id, ct);
        return Ok();
    }
    [HttpGet] public IActionResult Purge(string section, Guid id)
    {
        access.ValidateSection(section);
        return Confirm("Purge", "Purge", new { section, id }, "PurgeWarning");
    }
    [HttpPost, ActionName("Purge")] public async Task<IActionResult> PurgeConfirmed(string section, Guid id, DeletionRequestDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await access.PurgeAsync(section, id, ct, input.Reason);
        return Ok();
    }
}
