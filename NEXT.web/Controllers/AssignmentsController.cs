using NEXT.web.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.Security.RolePermissions;
using NEXT.Application.Features.Security.Roles;
using NEXT.web.Models;

namespace NEXT.web.Controllers;

public sealed class AssignmentsController(ISender sender,
    PermissionOptionsProvider permissionOptions) : WorkspaceController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await sender.Send(new GetRolePermissionsQuery(), ct);
        return Table(new("Assignments", "AssignmentsDescription", ["Role", "Code", "Module"],
            items.Select(x => new TableRow([x.RoleName ?? "—", x.Code, x.Module], [Delete(new { x.RoleId, x.PermissionId })])).ToArray(),
            new("AssignPermission", Route("Create")), TotalCount: items.Count));
    }
    [HttpGet] public async Task<IActionResult> Create(CancellationToken ct)
    {
        var roles = await sender.Send(new GetRolesQuery(), ct);
        var options = await permissionOptions.GetAsync(ct);
        return Form(new("AssignPermission", Route("Create"),
            [new("RoleId", "Role", Type: "select", Required: true, Options: roles.Select(x => new FieldOption(x.Id.ToString(), x.Name ?? "")).ToArray()),
             new("PermissionId", "Permissions", Type: "select", Required: true, Options: options)]));
    }
    [HttpPost] public async Task<IActionResult> Create(Guid roleId, Guid permissionId, CancellationToken ct)
    {
        if (!ModelState.IsValid || roleId == Guid.Empty || permissionId == Guid.Empty) return BadRequest();
        await sender.Send(new AssignPermissionToRoleCommand(new(roleId, permissionId)), ct);
        return Ok();
    }
    [HttpGet] public IActionResult Delete(Guid roleId, Guid permissionId) => Confirm("Delete", "Delete", new { roleId, permissionId });
    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(Guid roleId, Guid permissionId, CancellationToken ct)
    {
        await sender.Send(new RevokePermissionFromRoleCommand(roleId, permissionId), ct);
        return Ok();
    }
}
