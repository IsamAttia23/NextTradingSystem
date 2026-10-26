using System.Net;

namespace NEXT.Application.Exceptions;

public sealed class UnprocessableEntityException(string message)
    : AppException(message, HttpStatusCode.UnprocessableEntity);
