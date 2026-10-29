using MediatR;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Domain.Entities;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Services;

public sealed class PermissionsPresenter(ISender sender)

{
    public async Task<TableModel> IndexAsync(Func<string, object?, string> route, int page = 1)
    {
        var result = await sender.Send(new GetEntitiesQuery<PermissionDto>(new PaginationQuery { PageNumber = page, PageSize = 20 }));
        return new("Permissions", "PermissionsDescription", ["Code", "Module", "Description"],
            result.Data.Select(x => new TableRow([x.Code, x.Module, x.Description ?? "—"], [new("Edit", route("Edit", new { x.Id })), new("Delete", route("Delete", new { x.Id }), true)])).ToArray(),
            new("CreatePermission", route("Create", null)), result.PageNumber, result.TotalPages, result.TotalCount);
    }
    public ModalForm PermissionForm(string title, string action, PermissionDto? item = null) => new(title, action,
        [new("Code", "Code", item?.Code, Required: true, MaxLength: 120),
         new("Module", "Module", item?.Module, Required: true, MaxLength: 80),
         new("Description", "Description", item?.Description, "textarea", MaxLength: 500)]);
}
