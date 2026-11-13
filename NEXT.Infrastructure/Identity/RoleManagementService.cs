using NEXT.Infrastructure.Identity.Entities;
using NEXT.Infrastructure.Persistence.UsersManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;
using Mapster;

namespace NEXT.Infrastructure.Identity;

/// <summary>Infrastructure adapter for ASP.NET Core Identity role management.</summary>
public sealed class RoleManagementService(RoleManager<Role> roleManager, NEXTDbContext context, TypeAdapterConfig mapping) : IRoleManagementService
{
    public async Task<AccessRole?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await FindRoleAsync(id, cancellationToken))?.Adapt<AccessRole>(mapping);

    public Task<TDto?> FindByIdAsync<TDto>(Guid id, CancellationToken cancellationToken = default) where TDto : class =>
        StorageErrors.ExecuteAsync(() => roleManager.Roles.AsNoTracking().Where(role => role.Id == id)
            .ProjectToType<TDto>(mapping).FirstOrDefaultAsync(cancellationToken));

    public async Task<IReadOnlyList<TDto>> GetAllAsync<TDto>(CancellationToken cancellationToken = default) where TDto : class =>
        await StorageErrors.ExecuteAsync(() => roleManager.Roles.AsNoTracking().OrderBy(role => role.Name)
            .ProjectToType<TDto>(mapping).ToListAsync(cancellationToken));

    public async Task<IdentityOperationResult> CreateAsync(AccessRole role, CancellationToken ct = default)
    {
        var stored = new Role { Id = role.Id, Name = role.Name, Description = role.Description, IsSystem = role.IsSystem };
        var result = await IdentityCall.CompleteAsync(() => roleManager.CreateAsync(stored), ct);
        role.Id = stored.Id;
        role.NormalizedName = stored.NormalizedName;
        return Convert(result);
    }

    public async Task<IdentityOperationResult> UpdateAsync(AccessRole role, CancellationToken ct = default)
    {
        var stored = await RequireRoleAsync(role.Id, ct);
        stored.Name = role.Name;
        stored.Description = role.Description;
        var result = await IdentityCall.CompleteAsync(() => roleManager.UpdateAsync(stored), ct);
        role.NormalizedName = stored.NormalizedName;
        return Convert(result);
    }

    public async Task<IdentityOperationResult> DeleteAsync(AccessRole role, CancellationToken ct = default)
    {
        if (await StorageErrors.ExecuteAsync(() => context.UserRoles.AnyAsync(userRole => userRole.RoleId == role.Id, ct))
            || await StorageErrors.ExecuteAsync(() => context.RolePermissions.AnyAsync(rolePermission => rolePermission.RoleId == role.Id, ct)))
        {
            return IdentityOperationResult.Failure([
                new IdentityOperationError("RoleInUse", "The role is assigned to users or permissions and cannot be deleted.")
            ]);
        }
        var stored = await RequireRoleAsync(role.Id, ct);
        return Convert(await IdentityCall.CompleteAsync(() => roleManager.DeleteAsync(stored), ct));
    }

    private Task<Role?> FindRoleAsync(Guid id, CancellationToken ct) =>
        StorageErrors.ExecuteAsync(() => roleManager.Roles.FirstOrDefaultAsync(role => role.Id == id, ct));

    private async Task<Role> RequireRoleAsync(Guid id, CancellationToken ct) =>
        await FindRoleAsync(id, ct) ?? throw new NEXT.Application.Exceptions.NotFoundException("Role was not found.");

    public async Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        var normalized = roleManager.NormalizeKey(name.Trim());
        return await StorageErrors.ExecuteAsync(() => roleManager.Roles.AnyAsync(role =>
            role.NormalizedName == normalized && (!excludingId.HasValue || role.Id != excludingId.Value),
            cancellationToken));
    }

    private static IdentityOperationResult Convert(IdentityResult result) =>
        result.Succeeded ? IdentityOperationResult.Success :
        IdentityOperationResult.Failure(result.Errors.Select(error => new IdentityOperationError(error.Code, error.Description)));
}
