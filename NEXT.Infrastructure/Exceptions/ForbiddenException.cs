using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NEXT.Infrastructure.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message) : base(message, HttpStatusCode.Forbidden)
        {
        }
    }
}
