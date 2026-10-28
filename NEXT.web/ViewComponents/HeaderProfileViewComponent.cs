using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.ViewComponents;

public sealed class HeaderProfileViewComponent(ISender sender) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var id = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await sender.Send(new GetApplicationUserByIdQuery(id), HttpContext.RequestAborted);
        return View(new HeaderProfile(user?.FullName ?? User.Identity?.Name ?? "", user?.AvatarUrl is { Length: > 0 }));
    }
}
