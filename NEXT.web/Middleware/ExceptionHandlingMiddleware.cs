using NEXT.web.Models;
using Microsoft.Extensions.Localization;
using NEXT.web.Localization;
using Microsoft.AspNetCore.Mvc;
using NEXT.Application.Exceptions;
using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace NEXT.web.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IStringLocalizer<UiText>? text = null)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            // Localization runs downstream; its async culture does not flow back when an error bubbles out.
            if (context.Features.Get<IRequestCultureFeature>() is { } culture)
            {
                CultureInfo.CurrentUICulture = culture.RequestCulture.UICulture;
            }
            var status = exception is AppException appException ? StatusFor(appException.Code) : 500;
            if (status >= 500)
            {
                logger.LogError(exception, "Request {TraceIdentifier} failed", context.TraceIdentifier);
            }
            ProblemDetails problem = exception is AppValidationException validation
                            ? new ValidationProblemDetails(validation.Errors.ToDictionary(x => x.Key, x => x.Value.Select(value => text?[value].Value ?? value).ToArray()))
                            : new ProblemDetails();
            problem.Status = status;
            problem.Title = status >= 500 ? text?["UnexpectedError"].Value ?? "An unexpected error occurred."
                : text?[exception.Message].Value ?? exception.Message;
            if (exception is RecordRelationshipException)
            {
                var deleting = context.GetRouteValue("action")?.ToString() is "Delete" or "Purge";
                var key = deleting ? "DeleteLinkedRecord" : "RecordRelationshipBlocked";
                problem.Title = text?[key].Value ?? (deleting
                    ? "This item cannot be deleted because other records depend on it. Remove the relationships first, then try again."
                    : "This operation is blocked because this item is linked to other records.");
                problem.Extensions["userMessage"] = problem.Title;
            }
            problem.Extensions["traceId"] = context.TraceIdentifier;
            context.Response.Clear();
            context.Response.StatusCode = status;
            if (context.Request.Headers.Accept.Any(x => x?.Contains("text/html") == true)
                && context.Request.Headers["X-Requested-With"] != "XMLHttpRequest")
            {
                return; // The outer status-code middleware renders the localized error page.
            }
            await context.Response.WriteAsJsonAsync<object>(problem, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json",
                            cancellationToken: context.RequestAborted);
        }
    }
    private static int StatusFor(ApplicationErrorCode code) => code switch
    {
        ApplicationErrorCode.Validation => StatusCodes.Status400BadRequest,
        ApplicationErrorCode.NotFound => StatusCodes.Status404NotFound,
        ApplicationErrorCode.Conflict => StatusCodes.Status409Conflict,
        ApplicationErrorCode.RuleViolation => StatusCodes.Status422UnprocessableEntity,
        ApplicationErrorCode.DependencyUnavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError
    };
}
