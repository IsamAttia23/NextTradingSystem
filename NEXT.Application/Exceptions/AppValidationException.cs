using System.Net;

namespace NEXT.Application.Exceptions;

/// <summary>Represents validation failures that can be translated to a client error response.</summary>
public sealed class AppValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public AppValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", HttpStatusCode.BadRequest)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }
}
