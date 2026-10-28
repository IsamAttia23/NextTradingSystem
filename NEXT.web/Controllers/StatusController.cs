using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NEXT.web.Controllers;

[AllowAnonymous]
public sealed class StatusController : Controller
{
    [Route("Status/{code:int}")]
    public IActionResult Index(int code)
    {
        Response.StatusCode = code is >= 400 and <= 599 ? code : 500;
        return View(Response.StatusCode);
    }
}
