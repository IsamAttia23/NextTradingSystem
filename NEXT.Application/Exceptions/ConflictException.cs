

namespace NEXT.Application.Exceptions;

public sealed class ConflictException(string message, Exception? innerException = null) : AppException(message, ApplicationErrorCode.Conflict, innerException);
