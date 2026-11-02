

namespace NEXT.Application.Exceptions;

public sealed class DependencyUnavailableException(string message, Exception? innerException = null)
    : AppException(message, ApplicationErrorCode.DependencyUnavailable, innerException);
