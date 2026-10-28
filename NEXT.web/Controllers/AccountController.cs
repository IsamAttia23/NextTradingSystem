using NEXT.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

using NEXT.web.Localization;
using NEXT.Application.DTOs.Authentication;

namespace NEXT.web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(IAccountAccess access, IStringLocalizer<UiText> text) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginDto { ReturnUrl = returnUrl });
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginDto input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await access.LoginAsync(input);
        if (result == LoginOutcome.Succeeded) return LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : Url.Action("Index", "Home")!);
        if (result == LoginOutcome.RequiresTwoFactor) return RedirectToAction(nameof(Verify), new { input.ReturnUrl, input.RememberMe });
        ModelState.AddModelError("", text["LoginFailed"]);
        return View(input);
    }
    [AllowAnonymous, HttpGet] public async Task<IActionResult> Verify(string? returnUrl, bool rememberMe)
    {
        if (!await access.HasChallengeAsync()) return RedirectToAction(nameof(Login));
        return View(new VerifyLoginDto { ReturnUrl = returnUrl, RememberMe = rememberMe });
    }
    [AllowAnonymous, HttpPost] public async Task<IActionResult> Verify(VerifyLoginDto input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await access.VerifyAsync(input);
        if (result == LoginOutcome.MissingChallenge) return RedirectToAction(nameof(Login));
        if (result == LoginOutcome.Succeeded) return LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : Url.Action("Index", "Home")!);
        ModelState.AddModelError("", text["LoginFailed"]);
        return View(input);
    }
    [Authorize, HttpPost] public async Task<IActionResult> Logout()
    {
        await access.LogoutAsync();
        return RedirectToAction(nameof(Login));
    }
    [Authorize, HttpGet] public async Task<IActionResult> Avatar()
    {
        var image = await access.AvatarAsync();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return image is { Length: > 0 } ? File(image, "image/png") : Redirect(Url.Content("~/logo.svg"));
    }
    [AllowAnonymous] public IActionResult Denied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
