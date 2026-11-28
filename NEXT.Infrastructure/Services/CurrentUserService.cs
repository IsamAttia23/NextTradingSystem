using NEXT.Application.Abstractions.Context;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace NEXT.Infrastructure.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor, NEXT.Infrastructure.Stores.StoreContext? store = null) : ICurrentUserService
{
    public Guid? UserId

    {
        get
        {
            if (store?.IsProvisioning == true) return null;
            var raw = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }
}
