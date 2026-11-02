

namespace NEXT.Application.Exceptions;

public sealed class UnprocessableEntityException(string message, Exception? innerException = null)
    : AppException(message, ApplicationErrorCode.RuleViolation, innerException)
{
}
