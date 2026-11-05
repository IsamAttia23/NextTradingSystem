using NEXT.web.Models;
using MediatR;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Services;

public sealed class PermissionsPresenter(ISender sender, FormFieldPresenter fields)

{
    public async Task<TableModel> IndexAsync(Func<string, object?, string> route, int page = 1, CancellationToken ct = default)
    {
        var result = await sender.Send(new GetEntitiesQuery<PermissionDto>(new PaginationQuery
        {
            PageNumber = page,
            PageSize = 20
        }), ct);
        return new("Permissions", "PermissionsDescription", ["Code", "Module", "Description"],
            result.Data.Select(x => new TableRow([x.Code, x.Module, x.Description ?? "—"], [new("Edit", route("Edit", new { x.Id })), new("Delete", route("Delete", new { x.Id }), true)])).ToArray(),
            new("CreatePermission", route("Create", null)), result.PageNumber, result.TotalPages, result.TotalCount);
    }
    public async Task<ModalForm> PermissionFormAsync(string title, string action, PermissionDto? item = null, CancellationToken ct = default)
    {
        var formFields = item is null
            ? await fields.FormAsync(new PermissionCreationDto
            {
                Code = item?.Code ?? "",
                Module = item?.Module ?? "",
                Description = item?.Description
            }, ct, includedFields: ["Code", "Module", "Description"])
            : await fields.FormAsync(new PermissionUpdateDto
            {
                Code = item?.Code ?? "",
                Module = item?.Module ?? "",
                Description = item?.Description
            }, ct, includedFields: ["Code", "Module", "Description"]);
        return new(title, action, formFields);
    }
}
