using System.Net;

namespace NEXT.Infrastructure.Exceptions
{
    public class NotFoundException : AppException
    {
        public NotFoundException(string message) : base(message, HttpStatusCode.NotFound)
        {
        }
    }
}
