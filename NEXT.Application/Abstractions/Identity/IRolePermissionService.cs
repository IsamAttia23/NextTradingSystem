using NEXT.Application.DTOs.Security;

namespace NEXT.Application.Abstractions.Identity;

/// <summary>Specialized access for the composite RoleId/PermissionId key.</summary>
public interface IRolePermissionService
{
    Task<IReadOnlyList<RolePermissionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RolePermissionDto>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task AddAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);
}