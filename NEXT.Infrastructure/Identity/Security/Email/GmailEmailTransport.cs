using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NEXT.Infrastructure.Identity.Security.Contracts;
using NEXT.Infrastructure.Identity.Security.Options;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class GmailEmailTransport(HttpClient client, GmailAccessTokenProvider tokens,
    IOptions<GmailEmailOptions> options) : IEmailTransport
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
    {
        if (!options.Value.IsConfigured)
            throw new InvalidOperationException("Configure Email:Gmail before dispatching messages.");

        var raw = GmailMessageEncoder.Encode(options.Value.From, recipient, subject, body);
        var token = await tokens.GetAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { raw });
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            tokens.Invalidate(token);

        // The existing outbox handles retries; sending again here could duplicate a delivered message.
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gmail rejected the message (HTTP {(int)response.StatusCode}).");
    }
}
