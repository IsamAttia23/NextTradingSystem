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
        await StorageErrors.ExecuteAsync(() => ReadAssignments().OrderBy(x => x.RoleName).ThenBy(x => x.Code).ToListAsync(cancellationToken));

    public async Task<IReadOnlyList<RolePermissionDto>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        if (!await StorageErrors.ExecuteAsync(() => context.Roles.AnyAsync(x => x.Id == roleId, cancellationToken)))
        {
            throw new NotFoundException($"Role '{roleId}' was not found.");
        }
        return await StorageErrors.ExecuteAsync(() => ReadAssignments().Where(x => x.RoleId == roleId)
                    .OrderBy(x => x.Code).ToListAsync(cancellationToken));
    }

    public IQueryable<RolePermissionDto> ReadAssignments() =>
     from assignment in context.RolePermissions.AsNoTracking()
     join role in context.Roles on assignment.RoleId equals role.Id
     select new RolePermissionDto
     {
         RoleId = assignment.RoleId,
         PermissionId = assignment.PermissionId,
         RoleName = role.Name,
         Code = assignment.Permission.Code,
         Module = assignment.Permission.Module
     };

    public async Task AddAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        if (!await StorageErrors.ExecuteAsync(() => context.Roles.AnyAsync(x => x.Id == roleId, cancellationToken)))
        {
            throw new NotFoundException($"Role '{roleId}' was not found.");
        }
        if (!await StorageErrors.ExecuteAsync(() => context.Permissions.AnyAsync(x => x.Id == permissionId, cancellationToken)))
        {
            throw new NotFoundException($"Permission '{permissionId}' was not found.");
        }
        if (await StorageErrors.ExecuteAsync(() => context.RolePermissions.AnyAsync(x => x.RoleId == roleId
            && x.PermissionId == permissionId, cancellationToken)))
        {
            throw new AppValidationException(new Dictionary<string, string[]>
            {
                ["PermissionId"] = ["This permission is already assigned to the role."]
            });
        }
        context.RolePermissions.Add(new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        var assignment = await StorageErrors.ExecuteAsync(() => context.RolePermissions.FirstOrDefaultAsync(
            x => x.RoleId == roleId && x.PermissionId == permissionId, cancellationToken));
        if (assignment is null)
        {
            throw new NotFoundException("The role-permission assignment was not found.");
        }
        context.RolePermissions.Remove(assignment);
        await context.SaveChangesAsync(cancellationToken);
    }

}
