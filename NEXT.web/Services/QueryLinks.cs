using Microsoft.AspNetCore.WebUtilities;
namespace NEXT.web.Services;
public static class QueryLinks
{
    public static string Set(HttpRequest request, string key, string value, params string[] remove)
    {
        var values = request.Query.ToDictionary(x => x.Key, x => (string?)x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var name in remove) values.Remove(name);
        values[key] = value;
        return QueryHelpers.AddQueryString(request.PathBase + request.Path, values);
    }
}
