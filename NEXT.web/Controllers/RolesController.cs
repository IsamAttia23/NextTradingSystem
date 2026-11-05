using NEXT.web.Models;
using NEXT.Application.DTOs;
using NEXT.web.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.Security.Roles;

namespace NEXT.web.Controllers;

public sealed class RolesController(ISender sender, RolesPresenter presenter) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => Table(await presenter.IndexAsync((action, values) => Route(action, values), ct));

    [HttpGet] public async Task<IActionResult> Create(CancellationToken ct) => Form(await presenter.RoleFormAsync("CreateRole", Route("Create"), ct: ct));
    [HttpPost]
    public async Task<IActionResult> Create(RoleCreationDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        await sender.Send(new CreateRoleCommand(input), ct);
        return Ok();
    }
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var role = await sender.Send(new GetRoleByIdQuery(id), ct);
        return role is null ? NotFound() : Form(await presenter.RoleFormAsync("EditRole", Route("Edit", new
        {
            id
        }), role, ct));
    }
    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, RoleUpdateDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        await sender.Send(new UpdateRoleCommand(id, input), ct);
        return Ok();
    }
    [HttpGet] public IActionResult Delete(Guid id) => Confirm("Delete", "Delete", new { id });
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(Guid id, DeletionRequestDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        await sender.Send(new DeleteRoleCommand(id, input.Reason), ct);
        return Ok();
    }

}
