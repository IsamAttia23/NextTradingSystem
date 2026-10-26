using System.Net;

namespace NEXT.Infrastructure.Exceptions
{
    public class BadRequestException : AppException
    {
        public BadRequestException(string message) : base(message, HttpStatusCode.BadRequest)
        {
        }
    }
}
