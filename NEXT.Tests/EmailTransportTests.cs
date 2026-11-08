using NEXT.Infrastructure.Identity.Security.Email;
using NEXT.Infrastructure.Identity.Security.Options;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Xunit;

namespace NEXT.Tests;

public sealed class EmailTransportTests
{
    [Fact]
    public async Task HttpsTransportUsesTheProviderEndpointAndKeepsCredentialsOutOfErrors()
    {
        var handler = new CaptureHandler();
        using var client = new HttpClient(handler);
        var transport = new ResendEmailTransport(client, Options.Create(new ResendEmailOptions
        {
            ApiKey = "test-only-api-key", From = "sender@example.test"
        }));
        await transport.SendAsync("recipient@example.test", "Test", "Secret code 00427", default);
        Assert.Equal("https://api.resend.com/emails", handler.Url);
        Assert.Equal("Bearer test-only-api-key", handler.Authorization);
        var payload = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Secret code 00427", payload.RootElement.GetProperty("text").GetString());
        Assert.Equal("recipient@example.test", payload.RootElement.GetProperty("to")[0].GetString());
        handler.Status = HttpStatusCode.Unauthorized;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.SendAsync("recipient@example.test", "Test", "Secret code 00427", default));
        Assert.DoesNotContain("00427", error.Message);
        Assert.DoesNotContain("test-only-api-key", error.Message);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Authorization { get; private set; }
        public string? Body { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(Status) { Content = new StringContent("provider-private-error") };
        }
    }
}
