using NEXT.Infrastructure.Identity.Security.Contracts;
using NEXT.Infrastructure.Identity.Security.Options;
using Microsoft.Extensions.Options;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class ConfiguredEmailTransport(ResendEmailTransport api, SmtpEmailTransport smtp,
    IOptions<ResendEmailOptions> options, GmailEmailTransport gmail, IOptions<GmailEmailOptions> gmailOptions) : IEmailTransport
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
    {
        if (gmailOptions.Value.IsConfigured)
            return gmail.SendAsync(recipient, subject, body, ct);

        if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
            return api.SendAsync(recipient, subject, body, ct);

        return smtp.SendAsync(recipient, subject, body, ct);
    }
}
