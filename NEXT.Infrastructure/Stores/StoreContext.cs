using NEXT.Application.DTOs.Stores;
using NEXT.Application.Abstractions.Context;

namespace NEXT.Infrastructure.Stores;

public sealed class StoreContext(StoreServerSettings server) : ICurrentStore
{
    private StoreRegistration? selected;
    private bool bound;
    private bool connectionResolved;
    public Guid Id => selected?.Id ?? Guid.Empty;
    public string Key => IsDefault ? "root" : Id.ToString("N");
    public string? Slug => selected?.Slug;
    public string? Name => selected?.Name;
    public bool IsDefault => selected is null;
    internal bool IsProvisioning { get; private set; }

    internal void Select(StoreRegistration registration, bool provisioning = false)
    {
        if (bound || connectionResolved) throw new InvalidOperationException("The store cannot change inside a scope.");
        if (!provisioning && registration.State != StoreState.Active) throw new InvalidOperationException("Store is inactive.");
        _ = server.ConnectionFor(registration);
        selected = registration;
        IsProvisioning = provisioning;
        bound = true;
    }

    internal string ResolveConnection()
    {
        connectionResolved = true;
        return selected is null ? server.RootConnection : server.ConnectionFor(selected);
    }
}
