namespace NEXT.web.Models;

public sealed record PasswordFlowGrant(string Token, bool FirstLogin, DateTimeOffset ExpiresAt);
