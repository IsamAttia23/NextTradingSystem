using NEXT.Infrastructure.Identity.Security.Cryptography;
using NEXT.Application.Abstractions.Identity;
using NEXT.Domain.Entities;
using NEXT.Infrastructure.Persistence;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class SecurityEmailService(NEXTDbContext db, SecuritySecrets secrets, TimeProvider clock) : IEmailService
{
    public async Task QueueAsync(Guid userId, string recipient, string subject, string body,
        DateTimeOffset expiresAt, string credentialStamp, CancellationToken ct = default,
        Guid? otpChallengeId = null)
    {
        db.SecurityEmailMessages.Add(new SecurityEmailMessage
        {
            Id = Guid.NewGuid(), UserId = userId, Recipient = recipient, Subject = subject,
            EncryptedBody = secrets.Encrypt(body), CredentialStamp = credentialStamp,
            OtpChallengeId = otpChallengeId, CreatedAt = clock.GetUtcNow(),
            NextAttemptAt = clock.GetUtcNow(), ExpiresAt = expiresAt
        });
        await db.SaveChangesAsync(ct);
    }
}
