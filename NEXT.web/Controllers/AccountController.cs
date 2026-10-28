using NEXT.Application.Abstractions.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using NEXT.Domain.Entities;
using NEXT.web.Localization;
using NEXT.web.Models.Auth;

namespace NEXT.web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(SignInManager<ApplicationUser> signIn, IStringLocalizer<UiText> text, ILoginAccessPolicy access) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginInput { ReturnUrl = returnUrl });
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var user = await signIn.UserManager.FindByNameAsync(input.UserName);
        if (user is not null && await access.CanSignInAsync(user.Id))
        {
            var result = await signIn.PasswordSignInAsync(user, input.Password, input.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded) return LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : Url.Action("Index", "Home")!);
            if (result.RequiresTwoFactor)
            {
                return RedirectToAction(nameof(Verify), new { input.ReturnUrl, input.RememberMe });
            }
        }
        ModelState.AddModelError("", text["LoginFailed"]);
        return View(input);
    }
    [AllowAnonymous, HttpGet] public async Task<IActionResult> Verify(string? returnUrl, bool rememberMe)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null) return RedirectToAction(nameof(Login));
        return View(new VerifyLoginInput { ReturnUrl = returnUrl, RememberMe = rememberMe });
    }
    [AllowAnonymous, HttpPost] public async Task<IActionResult> Verify(VerifyLoginInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null || !await access.CanSignInAsync(user.Id)) return RedirectToAction(nameof(Login));
        var result = input.UseRecoveryCode
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(input.Code.Replace(" ", ""))
            : await signIn.TwoFactorAuthenticatorSignInAsync(input.Code.Replace(" ", "").Replace("-", ""), input.RememberMe, false);
        if (result.Succeeded) return LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : Url.Action("Index", "Home")!);
        ModelState.AddModelError("", text["LoginFailed"]);
        return View(input);
    }
    [Authorize, HttpPost] public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
    [AllowAnonymous] public IActionResult Denied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
