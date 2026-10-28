using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Services;

/// <summary>Persistence adapter for the RolePermission composite key.</summary>
public sealed class RolePermissionService(NEXTDbContext context) : IRolePermissionService
{
    public async Task<IReadOnlyList<RolePermissionDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.RolePermissions.AsNoTracking().OrderBy(x => x.Role.Name).ThenBy(x => x.Permission.Code)
            .Select(Projection).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RolePermissionDto>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return await context.RolePermissions.AsNoTracking().Where(x => x.RoleId == roleId)
            .OrderBy(x => x.Permission.Code).Select(Projection).ToListAsync(cancellationToken);
    }

    public Task<bool> RoleExistsAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        context.Roles.AnyAsync(x => x.Id == roleId, cancellationToken);

    public Task<bool> PermissionExistsAsync(Guid permissionId, CancellationToken cancellationToken = default) =>
        context.Permissions.AnyAsync(x => x.Id == permissionId, cancellationToken);

    public Task<bool> AssignmentExistsAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default) =>
        context.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken);

    public async Task AddAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        if (!await context.Roles.AnyAsync(x => x.Id == roleId, cancellationToken))
            throw new NotFoundException($"Role '{roleId}' was not found.");

        context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        var assignment = await context.RolePermissions.FirstOrDefaultAsync(
            x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken);
        if (assignment is null) return;
        context.RolePermissions.Remove(assignment);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static readonly System.Linq.Expressions.Expression<Func<RolePermission, RolePermissionDto>> Projection =
        x => new RolePermissionDto(x.RoleId, x.PermissionId, x.Role.Name, x.Permission.Code, x.Permission.Module);
}
