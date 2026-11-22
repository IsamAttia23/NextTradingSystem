using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NEXT.web.Filters;

/// <summary>Correlates MVC validation failures that do not pass through exception handling.</summary>
public sealed class RequestFailureFilter(ILogger<RequestFailureFilter> logger) : IAsyncAlwaysRunResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: ProblemDetails problem } result)
        {
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            if (problem is ValidationProblemDetails validation)
                logger.LogWarning("Request validation returned status {StatusCode}: {@ValidationErrors}",
                    result.StatusCode ?? problem.Status ?? 400, validation.Errors);
        }
        await next();
    }
}
