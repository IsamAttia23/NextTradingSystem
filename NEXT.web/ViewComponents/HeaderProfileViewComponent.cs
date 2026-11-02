using NEXT.web.Models;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.Application.DTOs.Presentation;

namespace NEXT.web.ViewComponents;

public sealed class HeaderProfileViewComponent(ISender sender) : ViewComponent
{
    private static readonly object ProfileKey = new();

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.Items[ProfileKey] is not Task<HeaderProfile> profile)
            HttpContext.Items[ProfileKey] = profile = LoadProfileAsync();
        return View(await profile);
    }

    private async Task<HeaderProfile> LoadProfileAsync()
    {
        var id = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id is null ? null : await sender.Send(new GetApplicationUserByIdQuery(id), HttpContext.RequestAborted);
        return new(user?.FullName ?? User.Identity?.Name ?? "", user?.AvatarUrl is { Length: > 0 });
    }
}
