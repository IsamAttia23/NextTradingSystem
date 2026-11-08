namespace NEXT.web.Models;

public sealed record OtpPageModel(string MaskedEmail, DateTimeOffset ExpiresAt, DateTimeOffset ResendAt);
