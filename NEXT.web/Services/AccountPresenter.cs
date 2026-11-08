using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using NEXT.Infrastructure.Identity.Security.Options;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.DTOs.ApplicationUsers;
using NEXT.Application.DTOs.Authentication;
using NEXT.Application.Features.AccountSecurity;
using NEXT.Application.Features.ApplicationUsers.Commands;
using NEXT.Application.Features.ApplicationUsers.Queries;
using NEXT.web.Localization;
using NEXT.web.Models;

namespace NEXT.web.Services;

public sealed class AccountPresenter(IAccountAccess access, ISender sender, UsersPresenter users,
    AccountFlowCookies flow, IHttpContextAccessor context, IModelMetadataProvider metadata,
    IStringLocalizer<UiText> text, IAntiforgery antiforgery, TimeProvider clock,
    IOptions<AccountSecurityOptions> securityOptions)
{
    public IActionResult Antiforgery() => new OkObjectResult(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    public IActionResult LoginPage(string? returnUrl) => View("Login", new LoginDto { ReturnUrl = returnUrl });
    public IActionResult ForgotPasswordPage() => View("ForgotPassword", new RecoveryEmailInput());
    public IActionResult LockedPage() => View("Locked");
    public IActionResult PasswordChangedPage() => View("PasswordChanged");

    public async Task<IActionResult> LoginAsync(LoginDto input, ModelStateDictionary state, IUrlHelper url, CancellationToken ct)
    {
        if (!state.IsValid) return View("Login", input, state);
        var outcome = await access.LoginAsync(input);
        switch (outcome)
        {
            case LoginOutcome.Succeeded:
                flow.Clear();
                return new LocalRedirectResult(url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : "/");
            case LoginOutcome.RequiresTwoFactor:
                return new RedirectToActionResult("Verify", "Account", new { input.ReturnUrl, input.RememberMe });
            case LoginOutcome.RequiresPasswordChange:
                var setup = await sender.Send(new FirstLoginCommand(input.UserName, input.Password), ct);
                if (setup.Outcome == LoginOutcome.AccountLocked) return Redirect("Locked");
                if (setup.SetupToken is not null)
                {
                    flow.SetGrant(new(setup.SetupToken, true, clock.GetUtcNow().AddMinutes(securityOptions.Value.GrantLifetimeMinutes)));
                    return Redirect("SetPassword");
                }
                break;
            case LoginOutcome.AccountLocked:
                return Redirect("Locked");
        }
        state.AddModelError("", text["LoginFailed"]);
        return View("Login", input, state);
    }

    public async Task<IActionResult> VerificationPageAsync(string? returnUrl, bool rememberMe)
    {
        if (!await access.HasChallengeAsync()) return Redirect("Login");
        return View("Verify", new VerifyLoginDto { ReturnUrl = returnUrl, RememberMe = rememberMe });
    }

    public async Task<IActionResult> VerifySecondFactorAsync(VerifyLoginDto input, ModelStateDictionary state, IUrlHelper url)
    {
        if (!state.IsValid) return View("Verify", input, state);
        var result = await access.VerifyAsync(input);
        if (result == LoginOutcome.MissingChallenge) return Redirect("Login");
        if (result == LoginOutcome.Succeeded)
            return new LocalRedirectResult(url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : "/");
        state.AddModelError("", text["LoginFailed"]);
        return View("Verify", input, state);
    }

    public async Task<IActionResult> RequestResetAsync(RecoveryEmailInput input, ModelStateDictionary state, CancellationToken ct)
    {
        if (!state.IsValid) return View("ForgotPassword", input, state);
        flow.Clear();
        var response = await sender.Send(new RequestPasswordResetCommand(input.Email), ct);
        SaveRecovery(response.ChallengeId, input.Email);
        return Redirect("VerifyOtp");
    }

    public IActionResult OtpPage()
    {
        var recovery = flow.GetRecovery();
        return recovery is null ? Redirect("ForgotPassword") : View("VerifyOtp", OtpModel(recovery));
    }

    public async Task<IActionResult> VerifyOtpAsync(OtpInput input, ModelStateDictionary state, CancellationToken ct)
    {
        var recovery = flow.GetRecovery();
        if (recovery is null) return Failure("RecoverySessionExpired", "expired", "/Account/ForgotPassword");
        if (!state.IsValid) return Failure("OtpFiveDigits", "invalid");
        var verified = await sender.Send(new VerifyPasswordResetOtpCommand(recovery.ChallengeId, input.Code), ct);
        if (!verified.Succeeded || verified.Grant is null)
        {
            if (Ajax) return Failure("OtpInvalidOrExpired", "invalid");
            state.AddModelError("", text["OtpInvalidOrExpired"]);
            return View("VerifyOtp", OtpModel(recovery), state);
        }
        flow.SetGrant(new(verified.Grant, false, clock.GetUtcNow().AddMinutes(securityOptions.Value.GrantLifetimeMinutes)));
        return Ajax ? new JsonResult(new { succeeded = true, redirectUrl = "/Account/SetPassword", message = text["OtpVerified"].Value })
            : Redirect("SetPassword");
    }

    public async Task<IActionResult> ResendOtpAsync(CancellationToken ct)
    {
        var recovery = flow.GetRecovery();
        if (recovery is null) return Failure("RecoverySessionExpired", "expired", "/Account/ForgotPassword");
        if (recovery.ResendAt > clock.GetUtcNow()) return Failure("WaitBeforeResend", "throttled");
        var response = await sender.Send(new RequestPasswordResetCommand(recovery.Email), ct);
        var updated = SaveRecovery(response.ChallengeId, recovery.Email);
        return new JsonResult(new { succeeded = true, message = text["ResetRequestNotice"].Value,
            expiresAt = updated.ExpiresAt, resendAt = updated.ResendAt });
    }

    public IActionResult PasswordPage() => flow.GetGrant() is { } grant
        ? View("SetPassword", new PasswordChangePageModel(grant.FirstLogin)) : Redirect("ForgotPassword");

    public async Task<IActionResult> SetPasswordAsync(PasswordChangeInput input, ModelStateDictionary state, CancellationToken ct)
    {
        var grant = flow.GetGrant();
        if (grant is null) return Redirect("ForgotPassword");
        if (!state.IsValid) return View("SetPassword", new PasswordChangePageModel(grant.FirstLogin), state);
        var result = await sender.Send(new ChangeVerifiedPasswordCommand(grant.Token, input.NewPassword, grant.FirstLogin), ct);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) state.AddModelError("", text[error.Description]);
            return View("SetPassword", new PasswordChangePageModel(grant.FirstLogin), state);
        }
        flow.Clear();
        await access.LogoutAsync();
        return Redirect("PasswordChanged");
    }

    public async Task<IActionResult> ProfilePageAsync(IUrlHelper url, CancellationToken ct) => View("Profile",
        await users.FormAsync(await sender.Send(new GetApplicationUserByIdQuery(
            HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct), (_, _) => url.Action("Profile", "Account")!, ct, true));

    public Task<UserOperationResult> UpdateProfileAsync(UserUpdateDto input, CancellationToken ct) =>
        sender.Send(new UpdateApplicationUserCommand("", input, ProfileOnly: true), ct);

    public async Task<IActionResult> LogoutAsync() { flow.Clear(); await access.LogoutAsync(); return Redirect("Login"); }

    public async Task<IActionResult> AvatarAsync()
    {
        HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
        var image = await access.AvatarAsync();
        return image is { Length: > 0 } ? new FileContentResult(image, "image/png") : new RedirectResult("/logo.svg");
    }


    private RecoveryFlow SaveRecovery(Guid challengeId, string email)
    {
        var state = new RecoveryFlow(challengeId, email, clock.GetUtcNow().AddMinutes(securityOptions.Value.OtpLifetimeMinutes), clock.GetUtcNow().AddSeconds(60));
        flow.SetRecovery(state);
        return state;
    }
    private static OtpPageModel OtpModel(RecoveryFlow state)
    {
        var at = state.Email.LastIndexOf('@');
        var masked = at > 0 ? state.Email[..1] + "***" + state.Email[at..] : "***";
        return new(masked, state.ExpiresAt, state.ResendAt);
    }
    private HttpContext HttpContext => context.HttpContext!;
    private bool Ajax => HttpContext.Request.Headers.XRequestedWith == "XMLHttpRequest";
    private JsonResult Failure(string key, string kind, string? redirectUrl = null) =>
        new(new { succeeded = false, kind, message = text[key].Value, redirectUrl }) { StatusCode = 400 };
    private static RedirectToActionResult Redirect(string action) => new(action, "Account", null);
    private ViewResult View(string name, object? model = null, ModelStateDictionary? state = null) => new()
    {
        ViewName = name,
        ViewData = new ViewDataDictionary(metadata, state ?? new ModelStateDictionary()) { Model = model }
    };
}
