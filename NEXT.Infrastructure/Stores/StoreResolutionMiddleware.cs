using NEXT.Application.DTOs.Stores;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace NEXT.Infrastructure.Stores;

public sealed partial class StoreResolutionMiddleware(RequestDelegate next)
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public async Task InvokeAsync(HttpContext context, StoreContext current, StoreServerSettings server, StoreCatalog catalog)
    {
        if (!context.Request.Path.StartsWithSegments("/s", out var rest)) { await next(context); return; }
        var path = rest.Value ?? "";
        var slash = path.IndexOf('/', 1);
        var slug = slash < 0 ? path.TrimStart('/') : path[1..slash];
        if (!server.Enabled || !SlugPattern().IsMatch(slug)) { await Unavailable(context); return; }
        var store = await catalog.FindSlugAsync(slug, context.RequestAborted);
        if (store is null || store.State != StoreState.Active) { await Unavailable(context); return; }
        current.Select(store);
        context.Request.PathBase = context.Request.PathBase.Add(new PathString("/s/" + slug));
        context.Request.Path = slash < 0 ? new PathString("/") : new PathString(path[slash..]);
        // The store is fixed for the whole request, including an outer status-page re-execution.
        // Keep its PathBase so error pages and their links remain in the selected workspace.
        await next(context);
    }

    private static async Task Unavailable(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("المحل غير متاح. تحقق من الرابط أو تواصل مع مدير النظام. / Store unavailable.");
    }
}
