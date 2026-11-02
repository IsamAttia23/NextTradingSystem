using MediatR;
using NEXT.Application.DTOs;
using NEXT.web.Services;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.Common.CQRS;

namespace NEXT.web.Controllers;

public sealed class PermissionsController(ISender sender, PermissionsPresenter presenter) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) => Table(await presenter.IndexAsync((action, values) => Route(action, values), page, ct));

    [HttpGet] public async Task<IActionResult> Create(CancellationToken ct) => Form(await presenter.PermissionFormAsync("CreatePermission", Route("Create"), ct: ct));
    [HttpPost] public async Task<IActionResult> Create(PermissionCreationDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new CreateEntityCommand<PermissionDto, PermissionCreationDto>(input), ct);
        return Ok();
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var item = await sender.Send(new GetEntityByIdQuery<PermissionDto>(id), ct);
        return item is null ? NotFound() : Form(await presenter.PermissionFormAsync("EditPermission", Route("Edit", new { id }), item, ct));
    }
    [HttpPost] public async Task<IActionResult> Edit(Guid id, PermissionUpdateDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new UpdateEntityCommand<PermissionDto, PermissionUpdateDto>(id, input), ct);
        return Ok();
    }
    [HttpGet] public IActionResult Delete(Guid id) => Confirm("Delete", "Delete", new { id });
    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(Guid id, DeletionRequestDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new DeleteEntityCommand<PermissionDto>(id, input.Reason), ct);
        return Ok();
    }

}
