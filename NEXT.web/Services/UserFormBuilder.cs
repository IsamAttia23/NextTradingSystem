using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class UserFormBuilder(IBranchLookup branches)
{
    public async Task<ModalForm> BuildAsync(UserDto? user, CancellationToken ct)
    {
        var prefix = user is null ? "Profile." : "";
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
        return new ModalForm(user is null ? "CreateUser" : "EditUser", user is null ? "/Users/Create" : "/Users/Edit/" + user.Id, fields,
            Message: user is null ? "PasswordHint" : null);
    }
}
