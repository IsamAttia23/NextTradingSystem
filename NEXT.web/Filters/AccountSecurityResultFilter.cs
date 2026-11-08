using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using NEXT.Application.Abstractions.Identity;
using NEXT.Application.Features.AccountSecurity;
using NEXT.web.Controllers;
using NEXT.web.Localization;

namespace NEXT.web.Filters;

public sealed class AccountSecurityResultFilter(IStringLocalizer<UiText> text) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Controller is AccountSecurityController && context.Result is ObjectResult result)
        {
            context.Result = result.Value switch
            {
                LoginPreparation { SetupToken: not null } login => new OkObjectResult(new { setupToken = login.SetupToken }),
                LoginPreparation => new UnauthorizedObjectResult(new { message = text["CredentialsNotVerified"].Value }),
                PasswordResetRequested reset => new OkObjectResult(new { challengeId = reset.ChallengeId, message = text["ResetRequestNotice"].Value }),
                OtpVerification { Succeeded: true } otp => new OkObjectResult(new { resetToken = otp.Grant }),
                OtpVerification => new BadRequestObjectResult(new { message = text["OtpInvalidOrExpired"].Value }),
                UserOperationResult operation => new ObjectResult(new
                {
                    operation.Succeeded, operation.RequiresSignIn,
                    errors = operation.Errors.Select(error => new { error.Code, Description = text[error.Description].Value })
                }) { StatusCode = operation.Succeeded ? 200 : 400 },
                _ => context.Result
            };
        }
        await next();
    }
}
