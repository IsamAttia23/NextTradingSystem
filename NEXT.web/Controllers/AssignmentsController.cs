using NEXT.Application.DTOs;
using NEXT.web.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Features.Security.RolePermissions;

namespace NEXT.web.Controllers;

public sealed class AssignmentsController(ISender sender, AssignmentsPresenter presenter) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => Table(await presenter.IndexAsync((action, values) => Route(action, values), ct));

    [HttpGet] public async Task<IActionResult> Create(CancellationToken ct) => Form(await presenter.CreateAsync(Route("Create"), ct));
    [HttpPost] public async Task<IActionResult> Create(Guid roleId, Guid permissionId, CancellationToken ct)
    {
        if (!ModelState.IsValid || roleId == Guid.Empty || permissionId == Guid.Empty) return BadRequest();
        await sender.Send(new AssignPermissionToRoleCommand(new(roleId, permissionId)), ct);
        return Ok();
    }
    [HttpGet] public IActionResult Delete(Guid roleId, Guid permissionId) => Confirm("Delete", "Delete", new { roleId, permissionId });
    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(Guid roleId, Guid permissionId, DeletionRequestDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new RevokePermissionFromRoleCommand(roleId, permissionId, input.Reason), ct);
        return Ok();
    }
}
