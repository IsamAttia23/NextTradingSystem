using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers;

public sealed class UserRoleAssignment(UserDtoReader reader, IUserCommandService users, IRoleManagementService roles,
    IUserManagementService management)
{
    internal static string[] Normalize(IEnumerable<string> values) =>
        values.Select(value => RequestValidation.Text(value, "Roles")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task<UserOperationResult> ReplaceAsync(Guid userId, IReadOnlyList<string> selectedRoles,
        IReadOnlyList<string> expectedRoles, CancellationToken ct)
    {
        RequestValidation.Identifier(userId, "UserId");
        var selected = Normalize(selectedRoles);
        foreach (var role in selected)
        {
            if (!await roles.NameExistsAsync(role, cancellationToken: ct))
            {
                RequestValidation.Fail("Roles", "RoleNotFound");
            }
        }
        return await management.ReplaceRolesAsync(userId, selected, Normalize(expectedRoles), ct);
    }

    public async Task<UserOperationResult> AssignAsync(Guid userId, string role, CancellationToken ct)
    {
        var name = RequestValidation.Text(role, "Role");
        var user = await reader.RequireAsync(userId, ct);
        if (!await roles.NameExistsAsync(name, cancellationToken: ct))
        {
            RequestValidation.Fail("Role", "Select an existing role.");
        }
        return await users.AddUserToRole(user, name, ct);
    }
}
