using Microsoft.Extensions.Localization;
using NEXT.web.Localization;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Exceptions;

namespace NEXT.web.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IStringLocalizer<UiText>? text = null)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            var status = exception is AppException appException ? (int)appException.StatusCode : 500;
            if (status >= 500)
                logger.LogError(exception, "Request {TraceIdentifier} failed", context.TraceIdentifier);
            ProblemDetails problem = exception is AppValidationException validation
                ? new ValidationProblemDetails(validation.Errors.ToDictionary(x => x.Key, x => x.Value.Select(value => text?[value].Value ?? value).ToArray()))
                : new ProblemDetails();
            problem.Status = status;
            problem.Title = status >= 500 ? "An unexpected error occurred." : exception.Message;
            problem.Extensions["traceId"] = context.TraceIdentifier;
            context.Response.Clear();
            context.Response.StatusCode = status;
            if (context.Request.Headers.Accept.Any(x => x?.Contains("text/html") == true)
                && context.Request.Headers["X-Requested-With"] != "XMLHttpRequest")
                return; // The outer status-code middleware renders the localized error page.
            await context.Response.WriteAsJsonAsync<object>(problem, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);
        }
    }
}
