using NEXT.Application.DTOs.Stores;

namespace NEXT.Application.Abstractions.Services;

public interface IStoreManagement
{
    bool IsAvailable { get; }
    Task<IReadOnlyList<TrialStoreView>> ListAsync(CancellationToken ct);
    Task<TrialStoreView?> FindAsync(Guid id, CancellationToken ct);
    Task<TrialStoreView> CreateAsync(CreateStoreInput input, Guid actorId, CancellationToken ct);
    Task SetActiveAsync(StoreActivationInput input, CancellationToken ct);
}
