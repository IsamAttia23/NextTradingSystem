using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Validation;

namespace NEXT.Application.Features.ApplicationUsers;

public sealed class UserRoleAssignment(UserDtoReader reader, IUserCommandService users, IRoleManagementService roles)
{
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
