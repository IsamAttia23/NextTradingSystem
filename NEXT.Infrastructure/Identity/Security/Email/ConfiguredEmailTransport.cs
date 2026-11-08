using NEXT.Infrastructure.Identity.Security.Contracts;
using NEXT.Infrastructure.Identity.Security.Options;
using Microsoft.Extensions.Options;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class ConfiguredEmailTransport(ResendEmailTransport api, SmtpEmailTransport smtp,
    IOptions<ResendEmailOptions> options) : IEmailTransport
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken ct) =>
        !string.IsNullOrWhiteSpace(options.Value.ApiKey)
            ? api.SendAsync(recipient, subject, body, ct)
            : smtp.SendAsync(recipient, subject, body, ct);
}
