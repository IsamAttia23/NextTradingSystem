namespace NEXT.Application.Abstractions.Identity;

public sealed record OtpVerification(bool Succeeded, string? Grant = null, bool AccountLocked = false);
