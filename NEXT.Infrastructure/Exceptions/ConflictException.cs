using System.Net;

namespace NEXT.Infrastructure.Exceptions
{
    public class ConflictException : AppException
    {
        public ConflictException(string message) : base(message,HttpStatusCode.Conflict)
        {
        }
    }
}
