using MediatR;

using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Domain.Entities;
using NEXT.Application.Features.Security.RolePermissions;
using NEXT.Application.Features.Security.Roles;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Services;

public sealed class AssignmentsPresenter(ISender sender)

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
        List<FieldOption> options = [];
        // Read all pages so permission choices are never silently truncated at 50.
        var page = 1;
        TPaginationQueryModel<PermissionDto> result;
        do
        {
            result = await sender.Send(new GetEntitiesQuery<PermissionDto>(new PaginationQuery { PageNumber = page++, PageSize = 50 }), ct);
            options.AddRange(result.Data.Select(x => new FieldOption(x.Id.ToString(), x.Module + " / " + x.Code)));
            ct.ThrowIfCancellationRequested();
        } while (page <= result.TotalPages);
        return new("AssignPermission", action,
            [new("RoleId", "Role", Type: "select", Required: true, Options: roles.Select(x => new FieldOption(x.Id.ToString(), x.Name ?? "")).ToArray()),
             new("PermissionId", "Permissions", Type: "select", Required: true, Options: options)]);
    }
}
