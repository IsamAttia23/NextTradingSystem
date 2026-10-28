using NEXT.Application.Exceptions;
using NEXT.Application.Abstractions.Media;

namespace NEXT.web.Services;

/// <summary>Bounded HTTP upload reading; decoding and content validation belong to the application port.</summary>
public static class ProfileUploadReader
{
    public static async Task<byte[]?> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null) return null;
        if (file.Length is <= 0 or > ProfileImageRules.MaxBytes)
            throw new AppValidationException(new Dictionary<string, string[]> { ["Avatar"] = ["ImageSizeError"] });
        await using var stream = file.OpenReadStream();
        using var result = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (result.Length + read > ProfileImageRules.MaxBytes)
                throw new AppValidationException(new Dictionary<string, string[]> { ["Avatar"] = ["ImageSizeError"] });
            await result.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return result.ToArray();
    }
}
