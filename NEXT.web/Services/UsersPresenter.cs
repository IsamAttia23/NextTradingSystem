using NEXT.web.Models;
using MediatR;

using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.Application.DTOs.Presentation;
using NEXT.Application.Features.Security.Roles;
using System.Globalization;
using System.Security.Claims;

namespace NEXT.web.Services;

public sealed class UsersPresenter(ISender sender, TimeProvider clock)

{
    public async Task<TableModel> IndexAsync(Func<string, object?, string> route, CancellationToken ct)
    {
        var users = (await sender.Send(new GetApplicationUsersQuery(), ct)).ToArray();
        return new("Users", "UsersDescription", ["FullName", "UserName", "Email", "Status"],
            users.Select(x =>
            {
                var locked = x.RequiresAdministratorUnlock || x.LockoutEnd > clock.GetUtcNow();
                var status = locked ? "AccountLocked" : x.IsActive ? "Active" : "Inactive";
                return new TableRow([x.FullName, x.UserName ?? "—", x.Email ?? "—", status],
                    [new("Edit", route("Edit", new { x.Id })), new("AssignRole", route("AssignRole", new { x.Id })), new("Delete", route("Delete", new { x.Id }), true)],
                    NameWarning: x.HasRole ? null : "UserHasNoRole",
                    Activation: new(x.IsActive, route("SetActive", new { x.Id }), x.FullName, locked));
            }).ToArray(),
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
            return new("EditProfile", route("Profile", null), fields, Message: "ProfilePasswordHint");
        }
        var options = (await sender.Send(new GetAssignableBranchesQuery(user?.BranchId), ct))
            .Select(x => new FieldOption(x.Id.ToString(), x.Name)).ToArray();
        fields.Add(new("BranchId", "Branch", user?.BranchId.ToString(), "select", true, Options: options));
        if (user is null) fields.Add(new("IsActive", "Active", "true", "checkbox"));
        if (user is null)
        {
            fields.Add(await RoleFieldAsync([], ct));
        }
        return new(user is null ? "CreateUser" : "EditUser", user is null ? route("Create", null) : route("Edit", new
        {
            user.Id
        }), fields,
            Message: user is null ? "TemporaryPasswordCreationHint" : null);
    }
    public async Task<ModalForm> AssignRoleAsync(Guid id, string action, CancellationToken ct)
    {
        var user = await sender.Send(new GetApplicationUserByIdQuery(id.ToString()), ct);
        if (user is null)
        {
            throw new NEXT.Application.Exceptions.NotFoundException("User was not found.");
        }
        var fields = new List<FormField> { await RoleFieldAsync(user.Roles, ct), new("RoleSelection", "Roles", "true", "hidden") };
        fields.AddRange(user.Roles.Select(role => new FormField("ExpectedRoles", "Roles", role, "hidden")));
        return new("AssignRole", action, fields);
    }

    private async Task<FormField> RoleFieldAsync(IReadOnlyList<string> selected, CancellationToken ct)
    {
        var roles = await sender.Send(new GetRolesQuery(), ct);
        var options = roles.Where(role => role.Name is not null)
            .Select(role => new FieldOption(role.Name!, role.Name!)).ToArray();
        return new("Roles", "Roles", Type: "checklist", Options: options, SelectedValues: selected);
    }

    public async Task<ProfileDetails> ProfileAsync(UserDto user, ClaimsPrincipal principal, string avatarUrl, CancellationToken ct)
    {
        var branches = await sender.Send(new GetAssignableBranchesQuery(user.BranchId), ct);
        var branch = branches.FirstOrDefault(option => option.Id == user.BranchId)?.Name;
        List<DetailFieldDto> fields =
        [
            new("UserName", user.UserName ?? "—"),
            new("Email", user.Email ?? "—"),
            new("PhoneNumber", user.PhoneNumber ?? "—"),
            new("Branch", branch ?? "—"),
            new("Roles", user.Roles.Count == 0 ? "—" : string.Join(", ", user.Roles)),
            new("Status", user.IsActive ? "Active" : "Inactive", TranslateValue: true),
            new("EmailVerified", user.EmailConfirmed ? "Yes" : "No", TranslateValue: true),
            new("PhoneVerified", user.PhoneNumberConfirmed ? "Yes" : "No", TranslateValue: true),
            new("TwoFactorEnabled", user.TwoFactorEnabled ? "Yes" : "No", TranslateValue: true),
            new("AccountCreatedOn", user.CreatedOn.ToLocalTime().ToString("g", CultureInfo.CurrentCulture))
        ];
        fields.AddRange(LoginHistory.Fields(user, principal));
        return new(new(user.FullName, user.AvatarUrl is { Length: > 0 } ? avatarUrl : null), fields);
    }
}
