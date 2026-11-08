namespace NEXT.Domain.Entities;

public sealed class SecurityEmailMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? OtpChallengeId { get; set; }
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string EncryptedBody { get; set; } = "";
    public string CredentialStamp { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? ClaimedUntil { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public int Attempts { get; set; }
}

