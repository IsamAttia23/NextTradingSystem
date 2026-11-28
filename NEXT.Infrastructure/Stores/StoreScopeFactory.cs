using Microsoft.Extensions.DependencyInjection;

namespace NEXT.Infrastructure.Stores;

public sealed class StoreScopeFactory(IServiceScopeFactory scopes)
{
    public AsyncServiceScope Create(StoreRegistration store)
    {
        var scope = scopes.CreateAsyncScope();
        try { scope.ServiceProvider.GetRequiredService<StoreContext>().Select(store); return scope; }
        catch { scope.Dispose(); throw; }
    }
}
