using NEXT.Application.DTOs.Common;
using NEXT.Application.DTOs;
using MediatR;
using NEXT.Application.Features.Common.CQRS;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.IServices;

using NEXT.Application.Common.QueryModels;
using NEXT.web.Services;
namespace NEXT.web.Controllers;
public abstract class EntityController<TDto, TInput, TQuery>(ISender sender, EntityPresenter<TDto, TInput> presenter)
    : WorkspaceController where TDto : class where TInput : class, new() where TQuery : PaginationQuery, new()
{
    private string Section => ControllerContext.ActionDescriptor.ControllerName;
    [HttpGet] public async Task<IActionResult> Index([FromQuery] TQuery query, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Table(await presenter.TableAsync(Section, (action, values) => Route(action, values), query, ct));
    }
    [HttpGet] public async Task<IActionResult> Create(CancellationToken ct) => Form(await presenter.FormAsync("Create", Route("Create"), null, ct));
    [HttpPost] public async Task<IActionResult> Create(TInput input, IFormFile? avatar, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (input is IProfileImageInput image) image.AvatarUrl = await ProfileUploadReader.ReadAsync(avatar, ct);
        await sender.Send(new CreateEntityCommand<TDto, TInput>(input), ct);
        return Ok();
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken ct) => Form(await presenter.FormAsync("Edit", Route("Edit", new { id }), id, ct));
    [HttpPost] public async Task<IActionResult> Edit(Guid id, TInput input, IFormFile? avatar, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (input is IProfileImageInput image) image.AvatarUrl = await ProfileUploadReader.ReadAsync(avatar, ct);
        await sender.Send(new UpdateEntityCommand<TDto, TInput>(id, input), ct);
        return Ok();
    }
    [HttpGet] public async Task<IActionResult> Details(Guid id, string? activityKind, string? returnUrl, int activityPage = 1, CancellationToken ct = default)
    {
        var model = await presenter.DetailsAsync(Section, id, activityKind, activityPage, (action, values) => Route(action, values), ct);
        return View("~/Views/Shared/RecordDetails.cshtml", model with { BackUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : model.BackUrl });
    }
    [HttpGet] public async Task<IActionResult> Avatar(Guid id, CancellationToken ct)
    {
        var item = await sender.Send(new GetEntityByIdQuery<TDto>(id), ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return item is IProfileImageInput { AvatarUrl: { Length: > 0 } bytes } ? File(bytes, "image/png") : NotFound();
    }
    [HttpGet] public IActionResult Delete(Guid id) => Confirm("Delete", "Delete", new { id });
    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(Guid id, DeletionRequestDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await sender.Send(new DeleteEntityCommand<TDto>(id, input.Reason), ct);
        return Ok();
    }
}
