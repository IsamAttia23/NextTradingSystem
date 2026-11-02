

namespace NEXT.Application.Exceptions;

public sealed class AppValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public AppValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", ApplicationErrorCode.Validation)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }
}
