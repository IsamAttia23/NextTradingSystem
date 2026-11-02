using MediatR;

using NEXT.Application.Abstractions.IServices;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.Security.RolePermissions;
using NEXT.Application.Features.Security.Roles;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Services;

public sealed class AssignmentsPresenter(ISender sender, IReferenceDataService references)

{
    public async Task<TableModel> IndexAsync(Func<string, object?, string> route, CancellationToken ct)
    {
        var items = await sender.Send(new GetRolePermissionsQuery(), ct);
        return new("Assignments", "AssignmentsDescription", ["Role", "Code", "Module"],
            items.Select(x => new TableRow([x.RoleName ?? "—", x.Code, x.Module], [new("Delete", route("Delete", new { x.RoleId, x.PermissionId }), true)])).ToArray(),
            new("AssignPermission", route("Create", null)), TotalCount: items.Count);
    }
    public async Task<ModalForm> CreateAsync(string action, CancellationToken ct)
    {
        var roles = await sender.Send(new GetRolesQuery(), ct);
        var options = (await references.GetAsync("Permissions", ct))
            .Select(x => new FieldOption(x.Value, x.Label)).ToArray();
        return new("AssignPermission", action,
            [new("RoleId", "Role", Type: "select", Required: true, Options: roles.Select(x => new FieldOption(x.Id.ToString(), x.Name ?? "")).ToArray()),
             new("PermissionId", "Permissions", Type: "select", Required: true, Options: options)]);
    }
}
