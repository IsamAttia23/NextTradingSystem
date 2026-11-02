namespace NEXT.Application.Abstractions.Media;

public interface IProfileImageProcessor
{
    byte[] Normalize(byte[] data);
    Task<byte[]> NormalizeAsync(byte[] data, CancellationToken ct = default);
}
