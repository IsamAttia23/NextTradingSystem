using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Features.Dashboard;
namespace NEXT.web.Controllers;
public sealed class HomeController(ISender sender) : WorkspaceController
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await sender.Send(new GetDashboardQuery(), ct));
}
