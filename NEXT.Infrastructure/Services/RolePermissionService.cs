using Microsoft.EntityFrameworkCore;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.Security;
using NEXT.Application.Exceptions;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Services;

/// <summary>Persistence adapter for the RolePermission composite key.</summary>
public sealed class RolePermissionService(NEXTDbContext context) : IRolePermissionService
{
    public async Task<IReadOnlyList<RolePermissionDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Query().OrderBy(x => x.RoleName).ThenBy(x => x.Code).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RolePermissionDto>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (!await context.Roles.AnyAsync(x => x.Id == roleId, cancellationToken))
            throw new NotFoundException($"Role '{roleId}' was not found.");
        return await Query().Where(x => x.RoleId == roleId).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        if (!await context.Roles.AnyAsync(x => x.Id == roleId, cancellationToken))
            throw new NotFoundException($"Role '{roleId}' was not found.");
        if (!await context.Permissions.AnyAsync(x => x.Id == permissionId, cancellationToken))
            throw new NotFoundException($"Permission '{permissionId}' was not found.");
        if (await context.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken))
            throw new AppValidationException(new Dictionary<string, string[]> { ["PermissionId"] = ["This permission is already assigned to the role."] });

        context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        var assignment = await context.RolePermissions.FirstOrDefaultAsync(
            x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken);
        if (assignment is null)
            throw new NotFoundException("The role-permission assignment was not found.");
        context.RolePermissions.Remove(assignment);
        await context.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<RolePermissionDto> Query() =>
        context.RolePermissions.AsNoTracking()
            .Select(x => new RolePermissionDto(x.RoleId, x.PermissionId, x.Role.Name, x.Permission.Code, x.Permission.Module));
}
