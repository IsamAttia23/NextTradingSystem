using NEXT.web.Models;
using MediatR;

using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Features.Security.Roles;

namespace NEXT.web.Services;

public sealed class UsersPresenter(ISender sender)

{
    public async Task<TableModel> IndexAsync(Func<string, object?, string> route, CancellationToken ct)
    {
        var users = (await sender.Send(new GetApplicationUsersQuery(), ct)).ToArray();
        return new("Users", "UsersDescription", ["FullName", "UserName", "Email", "Status"],
            users.Select(x => new TableRow([x.FullName, x.UserName ?? "—", x.Email ?? "—", x.IsActive ? "Active" : "Inactive"], [new("Edit", route("Edit", new { x.Id })), new("AssignRole", route("AssignRole", new { x.Id })), new("Delete", route("Delete", new { x.Id }), true)], NameWarning: x.HasRole ? null : "UserHasNoRole")).ToArray(),
            new("CreateUser", route("Create", null)), TotalCount: users.Length);
    }
    public async Task<ModalForm> FormAsync(UserDto? user, Func<string, object?, string> route, CancellationToken ct,
        bool profileOnly = false)
    {
        if (profileOnly && user is null)
        {
            throw new NEXT.Application.Exceptions.NotFoundException("User was not found.");
        }
        List<FormField> fields = [
                    new("Avatar", "ProfileImage", FormFieldPresenter.Avatar(user?.AvatarUrl), Type: "file"),
            new("FullName", "FullName", user?.FullName, Required: true, MaxLength: 200),
            new("UserName", "UserName", user?.UserName, Required: true, MaxLength: 256),
            new("Email", "Email", user?.Email, "email", true, 256),
            new("PhoneNumber", "PhoneNumber", user?.PhoneNumber, "tel", MaxLength: 32)];
        if (profileOnly)
        {
            fields.AddRange([
                new("CurrentPassword", "CurrentPassword", Type: "password", MaxLength: 128),
                new("NewPassword", "NewPassword", Type: "password", MaxLength: 128),
                new("ConfirmPassword", "ConfirmPassword", Type: "password", MaxLength: 128)]);
            return new("MyProfile", route("Profile", null), fields,
                Message: "ProfilePasswordHint", Inline: true);
        }
        var options = (await sender.Send(new GetAssignableBranchesQuery(user?.BranchId), ct))
            .Select(x => new FieldOption(x.Id.ToString(), x.Name)).ToArray();
        fields.Add(new("BranchId", "Branch", user?.BranchId.ToString(), "select", true, Options: options));
        fields.Add(new("IsActive", "Active", (user?.IsActive ?? true).ToString().ToLowerInvariant(), "checkbox"));
        if (user is null)
        {
            fields.Add(new("Password", "Password", Type: "password", Required: true, MaxLength: 128));
            var roles = await sender.Send(new GetRolesQuery(), ct);
            fields.Add(new("Role", "Role", Type: "select", Options: roles.Where(x => x.Name is not null)
                .Select(x => new FieldOption(x.Name!, x.Name!)).ToArray()));
        }
        return new(user is null ? "CreateUser" : "EditUser", user is null ? route("Create", null) : route("Edit", new
        {
            user.Id
        }), fields,
            Message: user is null ? "PasswordHint" : null);
    }
    public async Task<ModalForm> AssignRoleAsync(Guid id, string action, CancellationToken ct)
    {
        var user = await sender.Send(new GetApplicationUserByIdQuery(id.ToString()), ct);
        if (user is null)
        {
            throw new NEXT.Application.Exceptions.NotFoundException("User was not found.");
        }
        var roles = await sender.Send(new GetRolesQuery(), ct);
        return new("AssignRole", action,
            [new("Role", "Role", Type: "select", Required: true,
                Options: roles.Where(x => x.Name is not null && !user.Roles.Contains(x.Name))
                    .Select(x => new FieldOption(x.Name!, x.Name!)).ToArray())]);
    }
}
