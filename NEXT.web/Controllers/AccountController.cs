using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.DTOs.Authentication;
using NEXT.web.Models;
using NEXT.web.Services;

namespace NEXT.web.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(AccountPresenter presenter, StatusPresenter status) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => presenter.LoginPage(returnUrl);

    [AllowAnonymous, HttpPost]
    public Task<IActionResult> Login(LoginDto input, CancellationToken ct) => presenter.LoginAsync(input, ModelState, Url, ct);

    [AllowAnonymous, HttpGet]
    public Task<IActionResult> Verify(string? returnUrl, bool rememberMe) => presenter.VerificationPageAsync(returnUrl, rememberMe);

    [AllowAnonymous, HttpPost]
    public Task<IActionResult> Verify(VerifyLoginDto input) => presenter.VerifySecondFactorAsync(input, ModelState, Url);

    [AllowAnonymous, HttpGet]
    public IActionResult ForgotPassword() => presenter.ForgotPasswordPage();

    [AllowAnonymous, HttpPost]
    public Task<IActionResult> ForgotPassword(RecoveryEmailInput input, CancellationToken ct) => presenter.RequestResetAsync(input, ModelState, ct);

    [AllowAnonymous, HttpGet]
    public IActionResult VerifyOtp() => presenter.OtpPage();

    [AllowAnonymous, HttpPost]
    public Task<IActionResult> VerifyOtp(OtpInput input, CancellationToken ct) => presenter.VerifyOtpAsync(input, ModelState, ct);

    [AllowAnonymous, HttpPost]
    public Task<IActionResult> ResendOtp(CancellationToken ct) => presenter.ResendOtpAsync(ct);

    [AllowAnonymous, HttpGet]
    public IActionResult SetPassword() => presenter.PasswordPage();

    [AllowAnonymous, HttpPost]
    public Task<IActionResult> SetPassword(PasswordChangeInput input, CancellationToken ct) => presenter.SetPasswordAsync(input, ModelState, ct);

    [AllowAnonymous, HttpGet]
    public IActionResult Locked() => presenter.LockedPage();

    [AllowAnonymous, HttpGet]
    public IActionResult PasswordChanged() => presenter.PasswordChangedPage();

    [Authorize, HttpGet]
    public Task<IActionResult> Profile(CancellationToken ct) => presenter.ProfilePageAsync(Url, ct);

    [Authorize, HttpGet]
    public Task<IActionResult> EditProfile(CancellationToken ct) => presenter.EditProfileFormAsync(Url, ct);

    [Authorize, HttpPost]
    public Task<UserOperationResult> Profile([FromForm] UserUpdateDto input, CancellationToken ct) => presenter.UpdateProfileAsync(input, ct);

    [Authorize, HttpPost]
    public Task<IActionResult> Logout() => presenter.LogoutAsync();

    [Authorize, HttpGet]
    public Task<IActionResult> Avatar() => presenter.AvatarAsync();

    [AllowAnonymous]
    public IActionResult Denied() => status.Show(403);
}
