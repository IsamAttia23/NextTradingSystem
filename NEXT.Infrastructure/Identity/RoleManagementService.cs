using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity;

/// <summary>Infrastructure adapter for ASP.NET Core Identity role management.</summary>
public sealed class RoleManagementService(RoleManager<Role> roleManager, NEXTDbContext context) : IRoleManagementService
{
    public async Task<Role?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await roleManager.Roles.FirstOrDefaultAsync(role => role.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await roleManager.Roles.AsNoTracking().OrderBy(role => role.Name).ToListAsync(cancellationToken);

    public async Task<IdentityOperationResult> CreateAsync(Role role) => Convert(await roleManager.CreateAsync(role));
    public async Task<IdentityOperationResult> UpdateAsync(Role role) => Convert(await roleManager.UpdateAsync(role));

    public async Task<IdentityOperationResult> DeleteAsync(Role role)
    {
        if (await context.UserRoles.AnyAsync(userRole => userRole.RoleId == role.Id) ||
            await context.RolePermissions.AnyAsync(rolePermission => rolePermission.RoleId == role.Id))
        {
            return IdentityOperationResult.Failure([
                new IdentityOperationError("RoleInUse", "The role is assigned to users or permissions and cannot be deleted.")
            ]);
        }

        return Convert(await roleManager.DeleteAsync(role));
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        var normalized = roleManager.NormalizeKey(name.Trim());
        return await roleManager.Roles.AnyAsync(role =>
            role.NormalizedName == normalized && (!excludingId.HasValue || role.Id != excludingId.Value),
            cancellationToken);
    }

    private static IdentityOperationResult Convert(IdentityResult result) =>
        result.Succeeded ? IdentityOperationResult.Success :
        IdentityOperationResult.Failure(result.Errors.Select(error => new IdentityOperationError(error.Code, error.Description)));
}
