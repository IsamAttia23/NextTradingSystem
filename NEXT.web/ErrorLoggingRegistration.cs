using Serilog;
using Serilog.Events;
using System.Text;

namespace NEXT.web;

public static class ErrorLoggingRegistration
{
    public static IApplicationBuilder UseErrorRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.IncludeQueryInRequestPath = false;
            options.GetLevel = (context, _, error) => error is OperationCanceledException && context.RequestAborted.IsCancellationRequested
                ? LogEventLevel.Debug : error is not null || context.Response.StatusCode >= 500 ? LogEventLevel.Error
                : context.Response.StatusCode >= 400 ? LogEventLevel.Warning : LogEventLevel.Debug;
            options.EnrichDiagnosticContext = (diagnostics, context) => diagnostics.Set("ErrorReference", context.TraceIdentifier);
        });

    public static LoggerConfiguration ConfigureErrorLogging(this LoggerConfiguration logger,
        IConfiguration configuration, IHostEnvironment environment)
    {
        const string template = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{ErrorReference}] {SourceContext}{NewLine}" +
            "{Message:lj}{NewLine}{Properties:j}{NewLine}{Exception}{NewLine}";
        logger.MinimumLevel.Information().Enrich.FromLogContext();
        foreach (var setting in configuration.GetSection("Logging:LogLevel").GetChildren())
        {
            if (!Enum.TryParse<LogLevel>(setting.Value, out var level)) continue;
            var minimum = level switch
            {
                LogLevel.Trace => LogEventLevel.Verbose, LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Warning => LogEventLevel.Warning, LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical or LogLevel.None => LogEventLevel.Fatal, _ => LogEventLevel.Information
            };
            if (setting.Key == "Default") logger.MinimumLevel.Is(minimum);
            else logger.MinimumLevel.Override(setting.Key, minimum);
        }
        var directory = Path.GetFullPath(configuration["Logging:File:Directory"] ?? "Logs", environment.ContentRootPath);
        Directory.CreateDirectory(directory);
        return logger.WriteTo.Console(outputTemplate: template).WriteTo.File(
            Path.Combine(directory, "errors-.log"), restrictedToMinimumLevel: LogEventLevel.Warning,
            outputTemplate: template, encoding: new UTF8Encoding(false), rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true,
            retainedFileCountLimit: 30, shared: true);
    }
}
