namespace NEXT.Application.Abstractions.Identity;

public interface IOtpService
{
    Task<Guid> SendAsync(Guid userId, OtpPurpose purpose, CancellationToken ct = default);
    Task<OtpVerification> VerifyAsync(Guid challengeId, string code, OtpPurpose purpose,
        CancellationToken ct = default);
}
