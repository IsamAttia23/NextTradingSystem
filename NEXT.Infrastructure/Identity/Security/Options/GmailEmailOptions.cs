namespace NEXT.Infrastructure.Identity.Security.Options;

public sealed class GmailEmailOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string From { get; set; } = "";

    public bool IsEnabled => Values.Any(value => !string.IsNullOrWhiteSpace(value));
    public bool IsConfigured => Values.All(value => !string.IsNullOrWhiteSpace(value));

    private string[] Values => [ClientId, ClientSecret, RefreshToken, From];
}
