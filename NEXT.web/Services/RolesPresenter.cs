using MediatR;

using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.Security.Roles;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Services;

public sealed class RolesPresenter(ISender sender)

{
    public async Task<TableModel> IndexAsync(Func<string, object?, string> route, CancellationToken ct)
    {
        var roles = await sender.Send(new GetRolesQuery(), ct);
        return new("Roles", "RolesDescription", ["Name", "Description", "Type"],
            roles.Select(x => new TableRow([x.Name ?? "", x.Description ?? "—", x.IsSystem ? "System" : "Custom"],
                x.IsSystem ? [new("Edit", route("Edit", new { x.Id }))] : [new("Edit", route("Edit", new { x.Id })), new("Delete", route("Delete", new { x.Id }), true)])).ToArray(),
            new("CreateRole", route("Create", null)), TotalCount: roles.Count);
    }
    public ModalForm RoleForm(string title, string action, RoleDto? role = null) => new(title, action,
        [new("Name", "Name", role?.Name, Required: true, MaxLength: 50),
         new("Description", "Description", role?.Description, "textarea", MaxLength: 500)]);
}
