using NEXT.web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using NEXT.web.Controllers;
using NEXT.web.Localization;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.web.Filters;

/// <summary>Shared mutation responses and HTTP validation for the current user's profile.</summary>
public sealed class OperationNotificationFilter(IStringLocalizer<UiText> text, IAccountAccess access)
    : IAsyncActionFilter, IAsyncResultFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.Controller is AccountController &&
            context.RouteData.Values["action"]?.ToString() == "Profile" &&
            HttpMethods.IsPost(context.HttpContext.Request.Method) && !context.ModelState.IsValid)
        {
            var errors = context.ModelState.Where(x => x.Value!.Errors.Count > 0).ToDictionary(
                x => x.Key, x => x.Value!.Errors.Select(error =>
                    string.IsNullOrEmpty(error.ErrorMessage) ? text["ValidationFailed"].Value : text[error.ErrorMessage].Value).ToArray());
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors));
            return;
        }
        await next();
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: UserOperationResult operation })
        {
            if (!operation.Succeeded)
            {
                var errors = operation.Errors.GroupBy(error => error.Code).ToDictionary(
                    group => group.Key, group => group.Select(error => text[error.Description].Value).ToArray());
                context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors));
            }
            else
            {
                if (operation.RequiresSignIn) await access.LogoutAsync();
                var controller = (Controller)context.Controller;
                var message = text[operation.RequiresSignIn ? "ProfileSavedSignInAgain" : "UpdatedNotice"].Value;
                if (operation.Warnings.Count > 0)
                    message += " " + string.Join(" ", operation.Warnings.Select(x => text[x.Code].Value));
                context.Result = new JsonResult(new
                {
                    notification = new { kind = operation.Warnings.Count > 0 ? "warning" : "success", message },
                    redirectUrl = operation.RequiresSignIn
                        ? controller.Url.Action("Login", "Account", new { returnUrl = controller.Url.Action("Profile", "Account") })
                        : null
                });
            }
        }
        else if (context.Controller is WorkspaceController && context.Result is OkResult)
        {
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
        await next();
    }
}
