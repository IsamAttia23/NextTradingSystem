using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Features.AccountSecurity;
using NEXT.web.Models;
using NEXT.web.Services;

namespace NEXT.web.Controllers;

[ApiController]
[Route("api/account-security")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountSecurityController(ISender sender, AccountPresenter presenter) : ControllerBase
{
    [AllowAnonymous, HttpGet("antiforgery")]
    public IActionResult Antiforgery() => presenter.Antiforgery();

    [AllowAnonymous, HttpPost("first-login")]
    public Task<LoginPreparation> FirstLogin(FirstLoginRequest request, CancellationToken ct) =>
        sender.Send(new FirstLoginCommand(request.UserName, request.Password), ct);

    [AllowAnonymous, HttpPost("first-login/complete")]
    public Task<UserOperationResult> CompleteFirstLogin(SetPasswordRequest request, CancellationToken ct) =>
        sender.Send(new ChangeVerifiedPasswordCommand(request.Token, request.NewPassword, true), ct);

    [AllowAnonymous, HttpPost("password-reset/request")]
    public Task<PasswordResetRequested> RequestReset(ResetRequest request, CancellationToken ct) =>
        sender.Send(new RequestPasswordResetCommand(request.Email), ct);

    [AllowAnonymous, HttpPost("password-reset/verify")]
    public Task<OtpVerification> VerifyReset(VerifyOtpRequest request, CancellationToken ct) =>
        sender.Send(new VerifyPasswordResetOtpCommand(request.ChallengeId, request.Code), ct);

    [AllowAnonymous, HttpPost("password-reset/complete")]
    public Task<UserOperationResult> CompleteReset(SetPasswordRequest request, CancellationToken ct) =>
        sender.Send(new ChangeVerifiedPasswordCommand(request.Token, request.NewPassword, false), ct);

    [Authorize(Policy = "ManageSecurity"), HttpPost("users/{userId:guid}/unlock")]
    public Task<UserOperationResult> Unlock(Guid userId, CancellationToken ct) => sender.Send(new UnlockUserCommand(userId), ct);

    [Authorize(Policy = "ManageSecurity"), HttpPost("users/{userId:guid}/activation-otp")]
    [HttpPost("users/{userId:guid}/temporary-password")]
    public Task<UserOperationResult> Reissue(Guid userId, CancellationToken ct) => sender.Send(new ReissueActivationOtpCommand(userId), ct);
}
