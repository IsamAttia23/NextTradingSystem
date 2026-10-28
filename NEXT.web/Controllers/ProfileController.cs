using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NEXT.Domain.Entities;

namespace NEXT.web.Controllers;

/// <summary>Serves profile images independently from authentication actions.</summary>
[Authorize]
public sealed class ProfileController(SignInManager<ApplicationUser> signIn) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Avatar()
    {
        var user = await signIn.UserManager.GetUserAsync(User);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return user?.AvatarUrl is { Length: > 0 } image
            ? File(image, "image/png")
            : Redirect(Url.Content("~/logo.svg"));
    }
}
