using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace NEXT.web.Controllers;

public sealed class CultureController : Controller
{
    [HttpPost] public IActionResult Set(string culture, string? returnUrl)
    {
        if (culture is not ("ar" or "en")) return BadRequest();
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, HttpOnly = true,
                SameSite = SameSiteMode.Lax, Secure = Request.IsHttps });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
    }
}
