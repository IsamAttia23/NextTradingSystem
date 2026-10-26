using System.Net;

namespace NEXT.Application.Exceptions;

public sealed class NotFoundException(string message)
    : AppException(message, HttpStatusCode.NotFound)
{
}
