namespace NEXT.Application.Abstractions.Context;

public interface IRequestMetadataService
{
    string? IpAddress { get; }
    string? UserAgent { get; }
}
