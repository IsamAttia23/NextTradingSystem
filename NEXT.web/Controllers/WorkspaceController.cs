using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXT.web.Models;

namespace NEXT.web.Controllers;

/// <summary>Only shared HTTP presentation behavior; business rules remain in Application.</summary>
[Authorize(Policy = "ManageSecurity")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class WorkspaceController : Controller
{
    protected IActionResult Table(TableModel model) => View("~/Views/Shared/Table.cshtml", model);
    protected IActionResult Form(ModalForm model) => PartialView("~/Views/Shared/_ModalForm.cshtml", model);
    protected string Route(string action, object? values = null) => Url.Action(action, values)!;
    protected UiAction Edit(object values) => new("Edit", Route("Edit", values));
    protected UiAction Delete(object values) => new("Delete", Route("Delete", values), true);
    protected IActionResult Confirm(string title, string action, object values, string message = "DeleteWarning") =>
        Form(new(title, Route(action, values), [], title, message, title is "Delete" or "Purge"));
}
