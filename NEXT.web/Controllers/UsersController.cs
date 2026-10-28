using NEXT.web.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.web.Models;
using NEXT.Application.Features.Security.Roles;
using NEXT.web.Models.Users;

namespace NEXT.web.Controllers;

public sealed class UsersController(ISender sender, UserFormBuilder forms) : WorkspaceController
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var users = (await sender.Send(new GetApplicationUsersQuery(), ct)).ToArray();
        return Table(new("Users", "UsersDescription", ["FullName", "UserName", "Email", "Status"],
            users.Select(x => new TableRow([x.FullName, x.UserName ?? "—", x.Email ?? "—", x.IsActive ? "Active" : "Inactive"], [Edit(new { x.Id }), new("AssignRole", Route("AssignRole", new { x.Id }))])).ToArray(),
            new("CreateUser", Route("Create")), TotalCount: users.Length));
    }
    [HttpGet] public async Task<IActionResult> Create(CancellationToken ct) => await UserForm(null, ct);
    [HttpPost] public async Task<IActionResult> Create(CreateUserInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var p = input.Profile;
        return Operation(await sender.Send(new CreateApplicationUserCommand(new UserCreationDto
        {
            FullName = p.FullName, UserName = p.UserName, Email = p.Email, PhoneNumber = p.PhoneNumber,
            BranchId = p.BranchId, IsActive = p.IsActive, Password = input.Password,
            AvatarUrl = await ProfileUploadReader.ReadAsync(p.Avatar, ct)
        }), ct));
    }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var user = await sender.Send(new GetApplicationUserByIdQuery(id.ToString()), ct);
        return user is null ? NotFound() : await UserForm(user, ct);
    }
    [HttpPost] public async Task<IActionResult> Edit(Guid id, UserInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Operation(await sender.Send(new UpdateApplicationUserCommand(id.ToString(), new UserUpdateDto
        {
            FullName = input.FullName, UserName = input.UserName, Email = input.Email, PhoneNumber = input.PhoneNumber,
            BranchId = input.BranchId, IsActive = input.IsActive,
            AvatarUrl = await ProfileUploadReader.ReadAsync(input.Avatar, ct)
        }), ct));
    }
    private IActionResult Operation(UserOperationResult result)
    {
        if (result.Succeeded) return Ok();
        foreach (var error in result.Errors) ModelState.AddModelError(error.Code, error.Description);
        return ValidationProblem(ModelState);
    }
    [HttpGet] public async Task<IActionResult> AssignRole(Guid id, CancellationToken ct)
    {
        var user = await sender.Send(new GetApplicationUserByIdQuery(id.ToString()), ct);
        if (user is null) return NotFound();
        var roles = await sender.Send(new GetRolesQuery(), ct);
        return Form(new("AssignRole", Route("AssignRole", new { id }),
            [new("Role", "Role", Type: "select", Required: true,
                Options: roles.Where(x => x.Name is not null && !user.Roles.Contains(x.Name))
                    .Select(x => new FieldOption(x.Name!, x.Name!)).ToArray())]));
    }
    [HttpPost] public async Task<IActionResult> AssignRole(Guid id, string role, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Operation(await sender.Send(new AssignUserRoleCommand(id, role), ct));
    }
    private async Task<IActionResult> UserForm(UserDto? user, CancellationToken ct) =>
        Form(await forms.BuildAsync(user, ct));
}
