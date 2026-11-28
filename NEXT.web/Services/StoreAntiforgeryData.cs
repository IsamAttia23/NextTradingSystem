using Microsoft.AspNetCore.Antiforgery;
using NEXT.Application.Abstractions.Context;

namespace NEXT.web.Services;

public sealed class StoreAntiforgeryData : IAntiforgeryAdditionalDataProvider
{
    public string GetAdditionalData(HttpContext context) => context.RequestServices.GetRequiredService<ICurrentStore>().Key;
    public bool ValidateAdditionalData(HttpContext context, string additionalData)
    {
        var store = context.RequestServices.GetRequiredService<ICurrentStore>();
        return additionalData == store.Key || store.IsDefault && string.IsNullOrEmpty(additionalData);
    }
}
