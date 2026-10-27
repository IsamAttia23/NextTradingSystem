using NEXT.Application.Abstractions.Context;
using Microsoft.AspNetCore.Http;

namespace NEXT.Infrastructure.Services;

public sealed class RequestMetadataService(IHttpContextAccessor accessor) : IRequestMetadataService
{
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var value = accessor.HttpContext?.Request.Headers["User-Agent"].ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, 1000)];
        }
    }
}
