using System.Net;

namespace NEXT.Application.Exceptions;

/// <summary>Base for application-level exceptions that the presentation layer can translate to HTTP responses.</summary>
public abstract class AppException : Exception
{
    public HttpStatusCode StatusCode { get; }

    protected AppException(string message, HttpStatusCode statusCode) : base(message) =>
        StatusCode = statusCode;
}
