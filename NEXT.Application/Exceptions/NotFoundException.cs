

namespace NEXT.Application.Exceptions;

public sealed class NotFoundException(string message, Exception? innerException = null)
    : AppException(message, ApplicationErrorCode.NotFound, innerException)
{
}
