namespace NEXT.Domain.Entities;

public sealed class OtpChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Purpose { get; set; }
    public string SecretDigest { get; set; } = "";
    public string CredentialStamp { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public int FailedAttempts { get; set; }
}

