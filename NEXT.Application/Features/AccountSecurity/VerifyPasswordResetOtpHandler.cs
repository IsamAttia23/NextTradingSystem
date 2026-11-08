using MediatR;
using NEXT.Application.Abstractions.Identity;

namespace NEXT.Application.Features.AccountSecurity;

public sealed class VerifyPasswordResetOtpHandler(IOtpService otp) : IRequestHandler<VerifyPasswordResetOtpCommand, OtpVerification>
{
    public Task<OtpVerification> Handle(VerifyPasswordResetOtpCommand request, CancellationToken ct) =>
        otp.VerifyAsync(request.ChallengeId, request.Code, OtpPurpose.PasswordReset, ct);
}
