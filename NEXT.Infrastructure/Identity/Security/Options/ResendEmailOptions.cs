namespace NEXT.Infrastructure.Identity.Security.Options;

public sealed class ResendEmailOptions
{
    public string ApiKey { get; set; } = "";
    public string From { get; set; } = "";
}
