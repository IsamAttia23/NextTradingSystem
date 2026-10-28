using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.Security.Roles;
using NEXT.web.Models;

namespace NEXT.web.Controllers;

public sealed class RolesController(ISender sender) : WorkspaceController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var roles = await sender.Send(new GetRolesQuery(), ct);
        return Table(new("Roles", "RolesDescription", ["Name", "Description", "Type"],
            roles.Select(x => new TableRow([x.Name ?? "", x.Description ?? "—", x.IsSystem ? "System" : "Custom"],
                x.IsSystem ? [Edit(new { x.Id })] : [Edit(new { x.Id }), Delete(new { x.Id })])).ToArray(),
            new("CreateRole", Route("Create")), TotalCount: roles.Count));
    }
    [HttpGet] public IActionResult Create() => RoleForm("CreateRole", Route("Create"));
    [HttpPost] public async Task<IActionResult> Create(RoleInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new CreateRoleCommand(new(input.Name, input.Description)), ct);
        return Ok();
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var role = await sender.Send(new GetRoleByIdQuery(id), ct);
        return role is null ? NotFound() : RoleForm("EditRole", Route("Edit", new { id }), role);
    }
    [HttpPost] public async Task<IActionResult> Edit(Guid id, RoleInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new UpdateRoleCommand(id, new(input.Name, input.Description)), ct);
        return Ok();
    }
    [HttpGet] public IActionResult Delete(Guid id) => Confirm("Delete", "Delete", new { id });
    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteRoleCommand(id), ct);
        return Ok();
    }
    private IActionResult RoleForm(string title, string action, RoleDto? role = null) => Form(new(title, action,
        [new("Name", "Name", role?.Name, Required: true, MaxLength: 50),
         new("Description", "Description", role?.Description, "textarea", MaxLength: 500)]));
}
