using MediatR;

using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Features.Security.Roles;

namespace NEXT.web.Services;

public sealed class UsersPresenter(ISender sender, IBranchLookup branches)

{
    public async Task<TableModel> IndexAsync(Func<string, object?, string> route, CancellationToken ct)
    {
        var users = (await sender.Send(new GetApplicationUsersQuery(), ct)).ToArray();
        return new("Users", "UsersDescription", ["FullName", "UserName", "Email", "Status"],
            users.Select(x => new TableRow([x.FullName, x.UserName ?? "—", x.Email ?? "—", x.IsActive ? "Active" : "Inactive"], [new("Edit", route("Edit", new { x.Id })), new("AssignRole", route("AssignRole", new { x.Id })), new("Delete", route("Delete", new { x.Id }), true)])).ToArray(),
            new("CreateUser", route("Create", null)), TotalCount: users.Length);
    }
    public async Task<ModalForm> FormAsync(UserDto? user, Func<string, object?, string> route, CancellationToken ct)
    {
        var prefix = "";
        var options = (await branches.GetAsync(ct)).Where(x => x.IsActive || x.Id == user?.BranchId)
            .Select(x => new FieldOption(x.Id.ToString(), x.Name)).ToArray();
        List<FormField> fields = [
            new(prefix + "Avatar", "ProfileImage", user?.AvatarUrl is { Length: > 0 } avatar ? "data:image/png;base64," + Convert.ToBase64String(avatar) : null, Type: "file"),
            new(prefix + "FullName", "FullName", user?.FullName, Required: true, MaxLength: 200),
            new(prefix + "UserName", "UserName", user?.UserName, Required: true, MaxLength: 256),
            new(prefix + "Email", "Email", user?.Email, "email", true, 256),
            new(prefix + "PhoneNumber", "PhoneNumber", user?.PhoneNumber, "tel", MaxLength: 32),
            new(prefix + "BranchId", "Branch", user?.BranchId.ToString(), "select", true, Options: options),
            new(prefix + "IsActive", "Active", (user?.IsActive ?? true).ToString().ToLowerInvariant(), "checkbox")];
        if (user is null) fields.Add(new("Password", "Password", Type: "password", Required: true, MaxLength: 128));
        return new(user is null ? "CreateUser" : "EditUser", user is null ? route("Create", null) : route("Edit", new { user.Id }), fields,
            Message: user is null ? "PasswordHint" : null);
    }
    public async Task<ModalForm> AssignRoleAsync(Guid id, string action, CancellationToken ct)
    {
        var user = await sender.Send(new GetApplicationUserByIdQuery(id.ToString()), ct);
        if (user is null) throw new NEXT.Application.Exceptions.NotFoundException("User was not found.");
        var roles = await sender.Send(new GetRolesQuery(), ct);
        return new("AssignRole", action,
            [new("Role", "Role", Type: "select", Required: true,
                Options: roles.Where(x => x.Name is not null && !user.Roles.Contains(x.Name))
                    .Select(x => new FieldOption(x.Name!, x.Name!)).ToArray())]);
    }
}
