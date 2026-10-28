using NEXT.Application.DTOs;
using NEXT.web.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Features.ApplicationUsers.Queries;

namespace NEXT.web.Controllers;

public sealed class UsersController(ISender sender, UsersPresenter presenter) : WorkspaceController
{
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => Table(await presenter.IndexAsync((action, values) => Route(action, values), ct));

    [HttpGet] public async Task<IActionResult> Create(CancellationToken ct) => Form(await presenter.FormAsync(null, (action, values) => Route(action, values), ct));
    [HttpPost] public async Task<IActionResult> Create(UserCreationDto input, IFormFile? avatar, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Operation(await sender.Send(new CreateApplicationUserCommand(input with
        { AvatarUrl = await ProfileUploadReader.ReadAsync(avatar, ct) }), ct));
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var user = await sender.Send(new GetApplicationUserByIdQuery(id.ToString()), ct);
        return user is null ? NotFound() : Form(await presenter.FormAsync(user, (action, values) => Route(action, values), ct));
    }
    [HttpPost] public async Task<IActionResult> Edit(Guid id, UserUpdateDto input, IFormFile? avatar, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Operation(await sender.Send(new UpdateApplicationUserCommand(id.ToString(), input with
        { AvatarUrl = await ProfileUploadReader.ReadAsync(avatar, ct) }), ct));
    }
    [HttpGet] public IActionResult Delete(Guid id) => Confirm("Delete", "Delete", new { id });
    [HttpPost, ActionName("Delete")] public async Task<IActionResult> DeleteConfirmed(Guid id, DeletionRequestDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Operation(await sender.Send(new DeleteApplicationUserCommand(id, input.Reason), ct));
    }

    [HttpGet] public async Task<IActionResult> AssignRole(Guid id, CancellationToken ct) =>
        Form(await presenter.AssignRoleAsync(id, Route("AssignRole", new { id }), ct));
    [HttpPost] public async Task<IActionResult> AssignRole(Guid id, string role, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Operation(await sender.Send(new AssignUserRoleCommand(id, role), ct));
    }

}
