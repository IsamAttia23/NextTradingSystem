namespace NEXT.Infrastructure.Services;

public interface IRequestMetadataService
{
    string? IpAddress { get; }
    string? UserAgent { get; }
}
