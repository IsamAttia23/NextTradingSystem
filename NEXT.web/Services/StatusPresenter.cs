using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Diagnostics;
using NEXT.web.Localization;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class StatusPresenter(IHttpContextAccessor context, IModelMetadataProvider metadata,
    IStringLocalizer<UiText> text)
{
    public IActionResult Show(int code)
    {
        var status = code is >= 400 and <= 599 ? code : 500;
        var model = status switch
        {
            401 => new StatusPageModel(status, "SignInRequired", "SignInRequiredDetail"),
            403 => new StatusPageModel(status, "AccessDeniedTitle", "AccessDeniedDescription"),
            _ => new StatusPageModel(status, "RequestFailed", "ErrorDetail")
        };
        var request = context.HttpContext!.Request;
        var ajax = request.Headers.XRequestedWith == "XMLHttpRequest";
        var originalPath = context.HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath;
        // Navigation requests accept HTML; API and form requests retain structured errors.
        if (request.Path.StartsWithSegments("/api") ||
            originalPath?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true ||
            (ajax && !request.Headers.Accept.Any(x => x?.Contains("text/html") == true)))
        {
            var result = new ObjectResult(new ProblemDetails
            {
                Status = status, Title = text[model.TitleKey], Detail = text[model.DescriptionKey]
            }) { StatusCode = status };
            result.ContentTypes.Add("application/problem+json");
            return result;
        }
        return new ViewResult
        {
            ViewName = "~/Views/Status/Index.cshtml", StatusCode = status,
            ViewData = new ViewDataDictionary(metadata, new ModelStateDictionary()) { Model = model }
        };
    }
}
