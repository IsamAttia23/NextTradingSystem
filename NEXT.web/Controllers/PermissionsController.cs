using NEXT.Application.DTOs;
using NEXT.web.Services;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Security;

namespace NEXT.web.Controllers;

public sealed class PermissionsController(IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto> permissions, PermissionsPresenter presenter) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Index(int page = 1) => Table(await presenter.IndexAsync((action, values) => Route(action, values), page));

    [HttpGet] public IActionResult Create() => Form(presenter.PermissionForm("CreatePermission", Route("Create")));
    [HttpPost] public async Task<IActionResult> Create(PermissionCreationDto input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await permissions.Add(input);
        return Ok();
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id)
    {
        var item = await permissions.Details(id);
        return item is null ? NotFound() : Form(presenter.PermissionForm("EditPermission", Route("Edit", new { id }), item));
    }
    [HttpPost] public async Task<IActionResult> Edit(Guid id, PermissionUpdateDto input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await permissions.Update(id, input);
        return Ok();
    }
    [HttpGet] public IActionResult Delete(Guid id) => Confirm("Delete", "Delete", new { id });
    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(Guid id, DeletionRequestDto input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await permissions.Delete(id, input.Reason);
        return Ok();
    }

}
