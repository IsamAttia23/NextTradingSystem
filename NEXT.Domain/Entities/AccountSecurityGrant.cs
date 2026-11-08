namespace NEXT.Domain.Entities;

public sealed class AccountSecurityGrant
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string CredentialStamp { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

