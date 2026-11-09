using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NEXT.Infrastructure;
using NEXT.Infrastructure.Identity.Security.Email;
using NEXT.Infrastructure.Identity.Security.Options;
using Xunit;

namespace NEXT.Tests;

public sealed class GmailEmailTests
{
    [Theory]
    [InlineData("رمز التحقق الخاص بك", "رمز التحقق الخاص بك هو 00427.")]
    [InlineData("Your verification code", "Your verification code is 00427.")]
    public async Task SendsUtf8MimeThroughHttpsWithOAuthAndPreservesLeadingZeros(string subject, string body)
    {
        using var handler = new GmailHandler();
        using var client = new HttpClient(handler, false);
        using var tokens = new GmailAccessTokenProvider(new ClientFactory(handler), Settings(), new TestClock());
        var transport = new GmailEmailTransport(client, tokens, Settings());
        await transport.SendAsync("recipient@example.test", subject, body, default);

        var requests = handler.Requests.ToArray();
        Assert.Equal("https://oauth2.googleapis.com/token", requests[0].Url);
        Assert.Contains("grant_type=refresh_token", requests[0].Body);
        Assert.Contains("refresh_token=test-refresh-token", requests[0].Body);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages/send", requests[1].Url);
        Assert.Equal("Bearer access-token-1", requests[1].Authorization);
        using var payload = JsonDocument.Parse(requests[1].Body);
        var raw = payload.RootElement.GetProperty("raw").GetString()!;
        Assert.Matches("^[A-Za-z0-9_-]+$", raw);
        var mime = DecodeRaw(raw);
        Assert.Contains("From: =?UTF-8?B?TkVYVA==?= <sender@gmail.com>", mime);
        Assert.Contains("To: <recipient@example.test>", mime);
        Assert.Contains("Content-Type: text/plain; charset=utf-8", mime);
        Assert.Equal(subject, DecodeSubject(mime));
        Assert.Equal(body, DecodeBody(mime));
    }

    [Fact]
    public async Task CachesTokensAcrossConcurrentMessagesAndRefreshesBeforeExpiry()
    {
        using var handler = new GmailHandler();
        var clock = new TestClock();
        using var tokens = new GmailAccessTokenProvider(new ClientFactory(handler), Settings(), clock);
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => tokens.GetAsync(default)));
        Assert.All(results, token => Assert.Equal("access-token-1", token));
        Assert.Single(handler.Requests);
        clock.Now = clock.Now.AddSeconds(3541);
        Assert.Equal("access-token-2", await tokens.GetAsync(default));
        Assert.Equal(2, handler.Requests.Count);
        tokens.Invalidate(results[0]);
        Assert.Equal("access-token-2", await tokens.GetAsync(default));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task UnauthorizedSendInvalidatesTheCachedTokenWithoutSendingTwice()
    {
        using var handler = new GmailHandler { SendStatus = HttpStatusCode.Unauthorized };
        using var client = new HttpClient(handler, false);
        using var tokens = new GmailAccessTokenProvider(new ClientFactory(handler), Settings(), new TestClock());
        var transport = new GmailEmailTransport(client, tokens, Settings());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync("recipient@example.test", "Test", "00427", default));
        Assert.Contains("401", error.Message);
        Assert.Equal(2, handler.Requests.Count);
        handler.SendStatus = HttpStatusCode.OK;
        await transport.SendAsync("recipient@example.test", "Test", "00427", default);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal("Bearer access-token-2", handler.Requests.Last().Authorization);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErrorsNeverExposeTokensMessageBodiesOrProviderResponses(bool failAuthorization)
    {
        using var handler = new GmailHandler
        {
            TokenStatus = failAuthorization ? HttpStatusCode.BadRequest : HttpStatusCode.OK,
            SendStatus = HttpStatusCode.Forbidden
        };
        using var client = new HttpClient(handler, false);
        using var tokens = new GmailAccessTokenProvider(new ClientFactory(handler), Settings(), new TestClock());
        var transport = new GmailEmailTransport(client, tokens, Settings());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync("recipient@example.test", "Test", "00427", default));
        foreach (var secret in new[] { "00427", "test-client-secret", "test-refresh-token", "access-token-1", "private-provider-response", "recipient@example.test" })
            Assert.DoesNotContain(secret, error.ToString());
    }

    [Fact]
    public async Task EncodesLongArabicHeadersWithoutSplittingUnicodeCharacters()
    {
        using var handler = new GmailHandler();
        using var client = new HttpClient(handler, false);
        using var tokens = new GmailAccessTokenProvider(new ClientFactory(handler), Settings(), new TestClock());
        var transport = new GmailEmailTransport(client, tokens, Settings());
        var subject = string.Concat(Enumerable.Repeat("رسالة تحقق 🔒 ", 20));
        await transport.SendAsync("recipient@example.test", subject, "النص", default);
        using var payload = JsonDocument.Parse(handler.Requests.Last().Body);
        var mime = DecodeRaw(payload.RootElement.GetProperty("raw").GetString()!);
        Assert.Equal(subject, DecodeSubject(mime));
        var headers = mime.Split("\r\n\r\n")[0];
        Assert.All(Regex.Matches(headers, @"=\?UTF-8\?B\?[^?]+\?="), match => Assert.True(match.Length <= 75));
    }

    [Theory]
    [InlineData("recipient@example.test\r\nBcc: other@example.test", "Test")]
    [InlineData("recipient@example.test", "Test\r\nBcc: other@example.test")]
    public async Task RejectsHeaderInjectionBeforeMakingNetworkRequests(string recipient, string subject)
    {
        using var handler = new GmailHandler();
        using var client = new HttpClient(handler, false);
        using var tokens = new GmailAccessTokenProvider(new ClientFactory(handler), Settings(), new TestClock());
        var transport = new GmailEmailTransport(client, tokens, Settings());
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(recipient, subject, "00427", default));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"access_token\":\"private-token\",\"expires_in\":0}")]
    [InlineData("{\"private\":\"secret-response\"}")]
    public async Task MalformedTokenResponsesAreSanitizedAndNeverCached(string response)
    {
        using var handler = new GmailHandler { TokenResponse = response };
        using var tokens = new GmailAccessTokenProvider(new ClientFactory(handler), Settings(), new TestClock());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => tokens.GetAsync(default));
        Assert.DoesNotContain("private-token", error.ToString());
        Assert.DoesNotContain("secret-response", error.ToString());
        handler.TokenResponse = null;
        Assert.Equal("access-token-2", await tokens.GetAsync(default));
    }

    [Fact]
    public async Task CompleteGmailConfigurationTakesPriorityAndDisabledGmailPreservesResend()
    {
        using var handler = new GmailHandler();
        using var client = new HttpClient(handler, false);
        using var tokens = new GmailAccessTokenProvider(new ClientFactory(handler), Settings(), new TestClock());
        var gmailOptions = Settings();
        var resendOptions = Options.Create(new ResendEmailOptions { ApiKey = "resend-key", From = "sender@example.test" });
        var gmail = new GmailEmailTransport(client, tokens, gmailOptions);
        var resend = new ResendEmailTransport(client, resendOptions);
        var smtp = new SmtpEmailTransport(Options.Create(new SmtpEmailOptions()));
        var configured = new ConfiguredEmailTransport(resend, smtp, resendOptions, gmail, gmailOptions);
        await configured.SendAsync("recipient@example.test", "Test", "00427", default);
        Assert.Contains("gmail.googleapis.com", handler.Requests.Last().Url);
        configured = new ConfiguredEmailTransport(resend, smtp, resendOptions, gmail, Options.Create(new GmailEmailOptions()));
        await configured.SendAsync("recipient@example.test", "Test", "00427", default);
        Assert.Equal("https://api.resend.com/emails", handler.Requests.Last().Url);
    }

    [Fact]
    public void PartialConfigurationFailsValidationWithoutLeakingSecrets()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:NEXTDatabase"] = "Host=localhost;Database=unused",
            ["AccountSecurity:SecretKey"] = Convert.ToBase64String(new byte[32]),
            ["Email:Gmail:ClientSecret"] = "private-partial-secret"
        }).Build();
        using var services = new ServiceCollection().AddInfrastructureServices(configuration).BuildServiceProvider();
        var error = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<GmailEmailOptions>>().Value);
        Assert.Contains("Email:Gmail", error.Message);
        Assert.DoesNotContain("private-partial-secret", error.Message);
    }

    private static IOptions<GmailEmailOptions> Settings() => Options.Create(new GmailEmailOptions
    {
        ClientId = "test-client-id", ClientSecret = "test-client-secret",
        RefreshToken = "test-refresh-token", From = "NEXT <sender@gmail.com>"
    });

    private static string DecodeRaw(string raw)
    {
        var padded = raw.Replace('-', '+').Replace('_', '/').PadRight((raw.Length + 3) / 4 * 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private static string DecodeSubject(string mime)
    {
        var header = Regex.Match(mime, @"Subject: (.*(?:\r\n .*)*)\r\n").Groups[1].Value;
        return string.Concat(Regex.Matches(header, @"=\?UTF-8\?B\?([^?]+)\?=")
            .Select(match => Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value))));
    }

    private static string DecodeBody(string mime) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(mime.Split("\r\n\r\n", 2)[1]));

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    private sealed record CapturedRequest(string Url, string? Authorization, string Body);

    private sealed class GmailHandler : HttpMessageHandler
    {
        private int tokenRequests;
        public ConcurrentQueue<CapturedRequest> Requests { get; } = [];
        public HttpStatusCode TokenStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode SendStatus { get; set; } = HttpStatusCode.OK;
        public string? TokenResponse { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Enqueue(new(request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString(),
                await request.Content!.ReadAsStringAsync(ct)));
            if (request.RequestUri.Host == "oauth2.googleapis.com")
            {
                var count = Interlocked.Increment(ref tokenRequests);
                await Task.Delay(10, ct);
                return new(TokenStatus) { Content = new StringContent(TokenStatus == HttpStatusCode.OK
                    ? TokenResponse ?? JsonSerializer.Serialize(new { access_token = "access-token-" + count, expires_in = 3600 })
                    : "private-provider-response") };
            }
            return new(SendStatus) { Content = new StringContent("private-provider-response") };
        }
    }
}
