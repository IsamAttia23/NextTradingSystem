using NEXT.Infrastructure.Identity.Security.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace NEXT.Infrastructure.Identity.Security.Cryptography;

public sealed class SecuritySecrets(IOptions<AccountSecurityOptions> options)
{
    public static bool IsValidKey(string value)
    {
        try { return Convert.FromBase64String(value).Length >= 32; }
        catch (FormatException) { return false; }
    }

    public static string NewOtp() => RandomNumberGenerator.GetInt32(100_000)
        .ToString("D5", CultureInfo.InvariantCulture);

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public string Fingerprint(string value) => Convert.ToHexString(
        HMACSHA256.HashData(DeriveKey("otp"), Encoding.UTF8.GetBytes(value)));

    public static bool Matches(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var input = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = new byte[input.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(DeriveKey("email"), 16);
        aes.Encrypt(nonce, input, encrypted, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(encrypted).ToArray());
    }

    public string Decrypt(string ciphertext)
    {
        var bytes = Convert.FromBase64String(ciphertext);
        if (bytes.Length < 28) throw new CryptographicException("Invalid encrypted message.");
        var output = new byte[bytes.Length - 28];
        using var aes = new AesGcm(DeriveKey("email"), 16);
        aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), output);
        return Encoding.UTF8.GetString(output);
    }

    private byte[] DeriveKey(string purpose)
    {
        byte[] key;
        try { key = Convert.FromBase64String(options.Value.SecretKey); }
        catch (FormatException) { throw new InvalidOperationException("AccountSecurity:SecretKey must be base64."); }
        if (key.Length < 32)
            throw new InvalidOperationException("Configure AccountSecurity:SecretKey with at least 32 random bytes.");
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("NEXT.Security." + purpose));
    }
}
