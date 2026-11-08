using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXT.web.Services;

namespace NEXT.web.Controllers;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StatusController(StatusPresenter presenter) : Controller
{
    [Route("Status/{code:int}")]
    public IActionResult Index(int code) => presenter.Show(code);
}
