using System.Net;

namespace NEXT.Infrastructure.Exceptions;

public sealed class AppValidationException : AppException
{
    public AppValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.validation_failed", HttpStatusCode.BadRequest)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
