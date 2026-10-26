using Microsoft.AspNetCore.Http;
using NEXT.Infrastructure.Exceptions;
namespace NEXT.Infrastructure.Middleware
{

    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }

            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";

                if (ex is AppException appException)
                {
                    context.Response.StatusCode = (int)appException._statusCode;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = appException.Message
                    });
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = ex.Message
                    });
                }
            }
        }
    }
}
