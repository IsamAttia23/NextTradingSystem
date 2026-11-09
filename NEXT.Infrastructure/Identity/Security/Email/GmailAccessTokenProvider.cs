using System.Text.Json;
using Microsoft.Extensions.Options;
using NEXT.Infrastructure.Identity.Security.Options;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class GmailAccessTokenProvider(
    IHttpClientFactory clients, IOptions<GmailEmailOptions> options, TimeProvider clock) : IDisposable
{
    public const string HttpClientName = "GmailOAuth";
    private const int RefreshBufferSeconds = 60;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private string? accessToken;
    private DateTimeOffset refreshAt;

    public async Task<string> GetAsync(CancellationToken ct)
    {
        await refreshLock.WaitAsync(ct);
        try
        {
            if (accessToken is {} cached && clock.GetUtcNow() < refreshAt)
                return cached;

            var (token, seconds) = await RefreshAsync(ct);
            refreshAt = clock.GetUtcNow().AddSeconds(Math.Max(0, seconds - RefreshBufferSeconds));
            accessToken = token;
            return token;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async Task<(string Token, int ExpiresIn)> RefreshAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
            throw new InvalidOperationException("Configure Email:Gmail before dispatching messages.");

        using var client = clients.CreateClient(HttpClientName);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
            ["refresh_token"] = settings.RefreshToken,
            ["grant_type"] = "refresh_token"
        });
        using var response = await client.PostAsync("https://oauth2.googleapis.com/token", content, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gmail authorization failed (HTTP {(int)response.StatusCode}). Check the configured OAuth credentials.");

        return await ReadTokenAsync(response.Content, ct);
    }

    private static async Task<(string Token, int ExpiresIn)> ReadTokenAsync(HttpContent content, CancellationToken ct)
    {
        // Do not include OAuth responses or tokens in exceptions or logs.
        try
        {
            using var payload = JsonDocument.Parse(await content.ReadAsStringAsync(ct));
            var token = payload.RootElement.GetProperty("access_token").GetString();
            var seconds = payload.RootElement.GetProperty("expires_in").GetInt32();
            if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace) || seconds <= 0)
                throw new InvalidOperationException("Gmail returned an invalid access token response.");

            return (token, seconds);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            throw new InvalidOperationException("Gmail returned an invalid access token response.");
        }
    }

    public void Invalidate(string rejectedToken) =>
        Interlocked.CompareExchange(ref accessToken, null, rejectedToken);

    public void Dispose() => refreshLock.Dispose();
}
