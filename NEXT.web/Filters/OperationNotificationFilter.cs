using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using NEXT.web.Controllers;
using NEXT.web.Localization;

namespace NEXT.web.Filters;

/// <summary>One response contract for every successful workspace mutation.</summary>
public sealed class OperationNotificationFilter(IStringLocalizer<UiText> text) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Controller is not WorkspaceController || context.Result is not OkResult) return;
        var action = context.RouteData.Values["action"]?.ToString();
        var (kind, key) = action switch
        {
            "Create" => ("success", "CreatedNotice"),
            "Delete" or "Purge" => ("info", "DeletedNotice"),
            "Restore" => ("info", "RestoredNotice"),
            _ => ("info", "UpdatedNotice")
        };
        context.Result = new JsonResult(new { notification = new { kind, message = text[key].Value } });
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
