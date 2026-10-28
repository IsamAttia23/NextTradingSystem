using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Features.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using NEXT.web.Localization;
using NEXT.web.Models.Auth;

namespace NEXT.web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(AuthenticationService authentication, IStringLocalizer<UiText> text) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginInput { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await authentication.LoginAsync(input.UserName, input.Password, input.RememberMe);
        if (result == AuthenticationOutcome.Succeeded) return RedirectAfterLogin(input.ReturnUrl);
        if (result == AuthenticationOutcome.RequiresTwoFactor)
            return RedirectToAction(nameof(Verify), new { input.ReturnUrl, input.RememberMe });
        ModelState.AddModelError("", text["LoginFailed"]);
        return View(input);
    }

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Verify(string? returnUrl, bool rememberMe)
    {
        if (!await authentication.HasPendingTwoFactorUserAsync()) return RedirectToAction(nameof(Login));
        return View(new VerifyLoginInput { ReturnUrl = returnUrl, RememberMe = rememberMe });
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Verify(VerifyLoginInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await authentication.VerifyAsync(input.Code, input.UseRecoveryCode, input.RememberMe);
        if (result == AuthenticationOutcome.Succeeded) return RedirectAfterLogin(input.ReturnUrl);
        ModelState.AddModelError("", text["LoginFailed"]);
        return View(input);
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Logout()
    {
        await authentication.LogoutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult Denied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult RedirectAfterLogin(string? returnUrl) =>
        LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
}
