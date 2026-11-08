namespace NEXT.web.Models;

public sealed record RecoveryFlow(Guid ChallengeId, string Email, DateTimeOffset ExpiresAt, DateTimeOffset ResendAt);
