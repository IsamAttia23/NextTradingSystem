using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Identity;

public interface IRoleManagementService
{
    Task<Role?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> CreateAsync(Role role, CancellationToken ct = default);
    Task<IdentityOperationResult> UpdateAsync(Role role, CancellationToken ct = default);
    Task<IdentityOperationResult> DeleteAsync(Role role, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default);
}
