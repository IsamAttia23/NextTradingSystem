using NEXT.Domain.Entities;

namespace NEXT.Application.Abstractions.Identity;

/// <summary>Identity role operations, kept separate from generic CRUD because Role derives from IdentityRole.</summary>
public interface IRoleManagementService
{
    Task<Role?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> CreateAsync(Role role);
    Task<IdentityOperationResult> UpdateAsync(Role role);
    Task<IdentityOperationResult> DeleteAsync(Role role);
    Task<bool> NameExistsAsync(string name, Guid? excludingId = null, CancellationToken cancellationToken = default);
}
