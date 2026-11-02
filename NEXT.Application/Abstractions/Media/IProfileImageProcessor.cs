namespace NEXT.Application.Abstractions.Media;

public interface IProfileImageProcessor
{
    byte[] Normalize(byte[] data);
}
