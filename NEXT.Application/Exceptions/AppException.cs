

namespace NEXT.Application.Exceptions;

public abstract class AppException : Exception
{
    public ApplicationErrorCode Code { get; }

    protected AppException(string message, ApplicationErrorCode code, Exception? innerException = null) : base(message, innerException) =>
        Code = code;
}
