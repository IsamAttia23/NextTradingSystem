using System.Net;
namespace NEXT.Application.Exceptions;
public sealed class ConflictException(string message) : AppException(message, HttpStatusCode.Conflict);
