namespace NEXT.Infrastructure.Identity.Security.Contracts;

public interface IEmailTransport
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken ct);
}

