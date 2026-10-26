using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NEXT.Infrastructure.Exceptions
{
    public class InternalServerErorrException : AppException
    {
        public InternalServerErorrException(string message) : base(message, HttpStatusCode.InternalServerError)
        {
        }
    }
}
