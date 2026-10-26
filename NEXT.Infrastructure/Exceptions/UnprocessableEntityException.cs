using System.Net;

namespace NEXT.Infrastructure.Exceptions
{
    public class UnprocessableEntityException : AppException
    {
        public UnprocessableEntityException(string message) : base(message, HttpStatusCode.UnprocessableEntity)
        {
        }
    }
}
