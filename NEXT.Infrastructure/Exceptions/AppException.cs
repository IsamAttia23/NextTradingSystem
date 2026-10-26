using System.Net;

namespace NEXT.Infrastructure.Exceptions;

/// <summary>
/// Compatibility base for Infrastructure-specific exceptions. The shared exception contract lives in Application.
/// </summary>
public abstract class AppException : NEXT.Application.Exceptions.AppException
{
    public HttpStatusCode _statusCode => StatusCode;

    protected AppException(string message, HttpStatusCode statusCode)
        : base(message, statusCode)
    {
    }
}
