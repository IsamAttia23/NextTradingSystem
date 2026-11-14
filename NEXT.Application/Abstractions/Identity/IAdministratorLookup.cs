namespace NEXT.Application.Abstractions.Identity;

public interface IAdministratorLookup
{
    Task<bool> IsLastActiveAsync(Guid userId, CancellationToken ct);
    Task<bool> WouldRemoveLastActiveAsync(Guid userId, IReadOnlyList<string> remainingRoles, CancellationToken ct);
}
