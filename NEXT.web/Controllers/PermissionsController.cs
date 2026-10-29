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
    [HttpGet] public async Task<IActionResult> Index(int page = 1) => Table(await presenter.IndexAsync((action, values) => Route(action, values), page));

    [HttpGet] public IActionResult Create() => Form(presenter.PermissionForm("CreatePermission", Route("Create")));
    [HttpPost] public async Task<IActionResult> Create(PermissionCreationDto input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new CreateEntityCommand<PermissionDto, PermissionCreationDto>(input));
        return Ok();
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id)
    {
        var item = await sender.Send(new GetEntityByIdQuery<PermissionDto>(id));
        return item is null ? NotFound() : Form(presenter.PermissionForm("EditPermission", Route("Edit", new { id }), item));
    }
    [HttpPost] public async Task<IActionResult> Edit(Guid id, PermissionUpdateDto input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new UpdateEntityCommand<PermissionDto, PermissionUpdateDto>(id, input));
        return Ok();
    }
    [HttpGet] public IActionResult Delete(Guid id) => Confirm("Delete", "Delete", new { id });
    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(Guid id, DeletionRequestDto input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new DeleteEntityCommand<PermissionDto>(id, input.Reason));
        return Ok();
    }

}
