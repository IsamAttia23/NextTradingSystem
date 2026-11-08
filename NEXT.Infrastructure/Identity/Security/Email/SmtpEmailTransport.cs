using NEXT.Infrastructure.Identity.Security.Contracts;
using NEXT.Infrastructure.Identity.Security.Options;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace NEXT.Infrastructure.Identity.Security.Email;
public sealed class SmtpEmailTransport(IOptions<SmtpEmailOptions> options) : IEmailTransport
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.From))
            throw new InvalidOperationException("Configure Email:Smtp before dispatching messages.");
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = true, UseDefaultCredentials = false, Timeout = 30_000,
            Credentials = new NetworkCredential(settings.UserName, settings.Password)
        };
        using var message = new MailMessage(settings.From, recipient, subject, body);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await client.SendMailAsync(message, timeout.Token);
    }
}

