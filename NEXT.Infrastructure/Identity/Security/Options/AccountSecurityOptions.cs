namespace NEXT.Infrastructure.Identity.Security.Options;

public sealed class AccountSecurityOptions
{
    public string SecretKey { get; set; } = "";
    public int OtpLifetimeMinutes { get; set; } = 5;
    public int ActivationOtpLifetimeMinutes { get; set; } = 60;
    public int GrantLifetimeMinutes { get; set; } = 5;
}
