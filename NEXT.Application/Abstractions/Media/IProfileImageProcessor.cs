namespace NEXT.Application.Abstractions.Media;

public static class ProfileImageRules
{
    public const int MaxBytes = 7 * 1024 * 1024;
    public const uint MaxDimension = 12000;
    public const long MaxPixels = 40_000_000;
}

/// <summary>Verifies decoded image content and returns a metadata-free, bounded PNG avatar.</summary>
public interface IProfileImageProcessor
{
    byte[] Normalize(byte[] data);
}
