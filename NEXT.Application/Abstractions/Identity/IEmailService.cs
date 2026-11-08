namespace NEXT.Application.Abstractions.Identity;

public interface IEmailService
{
    Task QueueAsync(Guid userId, string recipient, string subject, string body,
        DateTimeOffset expiresAt, string credentialStamp, CancellationToken ct = default,
        Guid? otpChallengeId = null);
}
