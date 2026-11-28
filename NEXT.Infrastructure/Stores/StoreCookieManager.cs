using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using NEXT.Application.Abstractions.Context;
using Microsoft.Extensions.DependencyInjection;

namespace NEXT.Infrastructure.Stores;

public sealed class StoreCookieManager : ICookieManager
{
    private readonly ChunkingCookieManager cookies = new();
    private static string Name(HttpContext context, string key)
    {
        var store = context.RequestServices.GetRequiredService<ICurrentStore>();
        return store.IsDefault ? key : key + "." + store.Key;
    }
    private static void Scope(HttpContext context, CookieOptions options)
        => options.Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value : "/";
    public string? GetRequestCookie(HttpContext context, string key) => cookies.GetRequestCookie(context, Name(context, key));
    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options)
    { Scope(context, options); cookies.AppendResponseCookie(context, Name(context, key), value, options); }
    public void DeleteCookie(HttpContext context, string key, CookieOptions options)
    { Scope(context, options); cookies.DeleteCookie(context, Name(context, key), options); }
}
