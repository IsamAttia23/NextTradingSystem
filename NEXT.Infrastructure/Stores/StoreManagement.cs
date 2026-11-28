using NEXT.Application.Abstractions.Context;
using NEXT.Application.Abstractions.Services;
using NEXT.Application.DTOs.Stores;
using NEXT.Application.Exceptions;

namespace NEXT.Infrastructure.Stores;

public sealed class StoreManagement(StoreCatalog catalog, StoreProvisioningService provisioning,
    StoreServerSettings server, ICurrentStore current) : IStoreManagement
{
    public bool IsAvailable => current.IsDefault && server.Enabled;

    public async Task<IReadOnlyList<TrialStoreView>> ListAsync(CancellationToken ct)
    {
        RequireRoot();
        return (await catalog.ListAsync(ct)).Select(View).ToArray();
    }

    public async Task<TrialStoreView?> FindAsync(Guid id, CancellationToken ct)
    {
        RequireRoot();
        return await catalog.FindAsync(id, ct) is { } store ? View(store) : null;
    }

    public async Task<TrialStoreView> CreateAsync(CreateStoreInput input, Guid actorId, CancellationToken ct)
    {
        RequireRoot();
        return View(await provisioning.CreateAsync(input, actorId, ct));
    }

    public async Task SetActiveAsync(StoreActivationInput input, CancellationToken ct)
    {
        RequireRoot();
        var store = await catalog.FindAsync(input.Id, ct) ?? throw new NotFoundException("StoreUnavailable");
        if (store.State is not (StoreState.Active or StoreState.Disabled)) throw new ConflictException("StoreCreationUnavailable");
        if (store.Version != input.Version) throw new ConflictException("StoreChanged");
        if ((store.State == StoreState.Active) == input.Active) return;
        await catalog.ChangeStateAsync(input.Id, input.Active ? StoreState.Active : StoreState.Disabled, input.Version, ct);
    }

    private void RequireRoot()
    {
        if (!IsAvailable) throw new ConflictException("StoreCreationUnavailable");
    }

    private static TrialStoreView View(StoreRegistration store) => new(store.Id, store.Name, store.Slug,
        store.State, store.OwnerName, store.OwnerUserName, store.OwnerEmail, store.Version);
}
