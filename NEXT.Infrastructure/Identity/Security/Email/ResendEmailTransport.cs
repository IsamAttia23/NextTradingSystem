using NEXT.Infrastructure.Identity.Security.Contracts;
using NEXT.Infrastructure.Identity.Security.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class ResendEmailTransport(HttpClient client, IOptions<ResendEmailOptions> options) : IEmailTransport
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.From))
            throw new InvalidOperationException("Configure Email:Resend before dispatching messages.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new { from = settings.From, to = new[] { recipient }, subject, text = body });
        using var response = await client.SendAsync(request, ct);
        // Provider responses may contain recipient or message data; never include them in exceptions.
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("The email provider rejected the message.");
    }
}
