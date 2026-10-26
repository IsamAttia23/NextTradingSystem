using System.Net;

namespace NEXT.Infrastructure.Exceptions
{
    public abstract class AppException : Exception
    {
        public HttpStatusCode _statusCode { get; }

        protected AppException(string message, HttpStatusCode statusCode) : base(message)
        {
            _statusCode = statusCode;
        }
    }
}
