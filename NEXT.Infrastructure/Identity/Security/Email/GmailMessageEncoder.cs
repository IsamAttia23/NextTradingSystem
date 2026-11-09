using System.Net.Mail;
using System.Text;

namespace NEXT.Infrastructure.Identity.Security.Email;

internal static class GmailMessageEncoder
{
    private const int HeaderChunkBytes = 45;

    public static string Encode(string from, string recipient, string subject, string body)
    {
        var fromHeader = AddressHeader(from);
        var toHeader = AddressHeader(recipient);
        var subjectHeader = EncodeHeader(subject);
        var encodedBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(body), Base64FormattingOptions.InsertLineBreaks);
        var message = string.Join("\r\n",
            $"From: {fromHeader}",
            $"To: {toHeader}",
            $"Subject: {subjectHeader}",
            "MIME-Version: 1.0",
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: base64",
            "", encodedBody, "");

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(message)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string AddressHeader(string value)
    {
        RejectNewLines(value);
        if (!MailAddress.TryCreate(value, out var address))
            throw new InvalidOperationException("The Gmail sender or recipient address is invalid.");

        return string.IsNullOrEmpty(address.DisplayName)
            ? $"<{address.Address}>"
            : $"{EncodeHeader(address.DisplayName)} <{address.Address}>";
    }

    private static string EncodeHeader(string value)
    {
        RejectNewLines(value);
        List<string> chunks = [];
        var chunk = new StringBuilder();
        var size = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (size + rune.Utf8SequenceLength > HeaderChunkBytes)
            {
                chunks.Add(chunk.ToString());
                chunk.Clear();
                size = 0;
            }
            chunk.Append(rune.ToString());
            size += rune.Utf8SequenceLength;
        }
        if (chunk.Length > 0) chunks.Add(chunk.ToString());
        return string.Join("\r\n ", chunks.Select(text => $"=?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(text))}?="));
    }

    private static void RejectNewLines(string value)
    {
        if (value.Contains('\r') || value.Contains('\n'))
            throw new InvalidOperationException("Email headers cannot contain line breaks.");
    }
}
