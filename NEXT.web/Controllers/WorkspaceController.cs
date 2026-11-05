using NEXT.web.Models;
using Microsoft.AspNetCore.Authorization;
using NEXT.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.Controllers;

/// <summary>Only shared HTTP presentation behavior; business rules remain in Application.</summary>
[Authorize(Policy = "ManageSecurity")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class WorkspaceController : Controller
{
    protected IActionResult Table(TableModel model) => View("~/Views/Shared/Table.cshtml", model);
    protected IActionResult Form(ModalForm model) => PartialView("~/Views/Shared/_ModalForm.cshtml", model);
    protected string Route(string action, object? values = null) => Url.Action(action, values)!;
    protected IActionResult Confirm(string title, string action, object values, string message = "DeleteWarning") =>
        Form(new(title, Route(action, values), title is "Delete" or "Purge" ? [new("Reason", "DeletionReason", Type: "textarea", Required: true, MaxLength: 500)] : [], title, message, title is "Delete" or "Purge"));
    protected IActionResult Operation(UserOperationResult result)
    {
        if (result.Succeeded)
        {
            if (result.Warnings.Count == 0)
            {
                return Ok();
            }
            var text = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<NEXT.web.Localization.UiText>>();
            return Ok(new
            {
                notification = new
                {
                    kind = "warning",
                    message = string.Join(" ", result.Warnings.Select(x => text[x.Code].Value))
                }
            });
        }
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }
        return ValidationProblem(ModelState);
    }

}
