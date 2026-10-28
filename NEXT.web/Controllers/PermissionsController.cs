using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.IServices;
using NEXT.Application.Common.QueryModels;
using NEXT.Application.DTOs.Security;
using NEXT.web.Models;

namespace NEXT.web.Controllers;

public sealed class PermissionsController(IEntityServices<Guid, PermissionDto, PermissionCreationDto, PermissionUpdateDto> permissions) : WorkspaceController
{
    public async Task<IActionResult> Index(int page = 1)
    {
        var result = await permissions.GetAll(new PaginationQuery { PageNumber = page, PageSize = 20 });
        return Table(new("Permissions", "PermissionsDescription", ["Code", "Module", "Description"],
            result.Data.Select(x => new TableRow([x.Code, x.Module, x.Description ?? "—"], [Edit(new { x.Id }), Delete(new { x.Id })])).ToArray(),
            new("CreatePermission", Route("Create")), result.PageNumber, result.TotalPages, result.TotalCount));
    }
    [HttpGet] public IActionResult Create() => PermissionForm("CreatePermission", Route("Create"));
    [HttpPost] public async Task<IActionResult> Create(PermissionInput input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await permissions.Add(new() { Code = input.Code, Module = input.Module, Description = input.Description });
        return Ok();
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id)
    {
        var item = await permissions.Details(id);
        return item is null ? NotFound() : PermissionForm("EditPermission", Route("Edit", new { id }), item);
    }
    [HttpPost] public async Task<IActionResult> Edit(Guid id, PermissionInput input)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await permissions.Update(id, new() { Id = id, Code = input.Code, Module = input.Module, Description = input.Description });
        return Ok();
    }
    [HttpGet] public IActionResult Delete(Guid id) => Confirm("Delete", "Delete", new { id });
    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        await permissions.Delete(id);
        return Ok();
    }
    private IActionResult PermissionForm(string title, string action, PermissionDto? item = null) => Form(new(title, action,
        [new("Code", "Code", item?.Code, Required: true, MaxLength: 120),
         new("Module", "Module", item?.Module, Required: true, MaxLength: 80),
         new("Description", "Description", item?.Description, "textarea", MaxLength: 500)]));
}
