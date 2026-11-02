namespace NEXT.Application.Abstractions.Identity;
public interface IAdministratorLookup
{
    Task<bool> IsLastActiveAsync(Guid userId, CancellationToken ct);
}
