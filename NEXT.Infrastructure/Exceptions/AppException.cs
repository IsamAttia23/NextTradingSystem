namespace NEXT.Infrastructure.Exceptions;

/// <summary>
/// Compatibility base for Infrastructure-specific exceptions. The shared exception contract lives in Application.
/// </summary>
public abstract class AppException : NEXT.Application.Exceptions.AppException
{
    protected AppException(string message, System.Net.HttpStatusCode statusCode)
        : base(message, statusCode)
    {
    }
}
