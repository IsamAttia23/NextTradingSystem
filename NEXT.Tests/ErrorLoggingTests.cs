using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NEXT.Application.Exceptions;
using NEXT.web;
using NEXT.web.Middleware;
using Serilog;
using Xunit;

namespace NEXT.Tests;

public sealed class ErrorLoggingTests
{
    [Fact]
    public async Task RuntimePipelineRecordsRequestCompletionWithTheDisplayedReference()
    {
        var environment = new TestEnvironment();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddLocalization();
        builder.Services.AddSerilog((_, logger) => logger.ConfigureErrorLogging(Configuration(environment.ContentRootPath), environment));
        await using var app = builder.Build();
        app.UseErrorRequestLogging();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.Run(_ => throw new InvalidOperationException("Injected PDF request failure"));
        await app.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        using var response = await client.GetAsync("/InvoiceDocuments/Pdf?token=private-runtime-value");
        var body = await response.Content.ReadAsStringAsync();
        var reference = JsonDocument.Parse(body).RootElement.GetProperty("traceId").GetString();
        Assert.NotNull(reference);
        await app.StopAsync();
        var log = await ReadLogs(environment.ContentRootPath);
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(reference, log);
        Assert.Contains("HTTP GET /InvoiceDocuments/Pdf responded 500", log);
        Assert.Contains("Injected PDF request failure", log);
        Assert.DoesNotContain("private-runtime-value", log);
    }

    [Fact]
    public async Task FileLogRetainsNestedPdfFailureAndCorrelatesItWithoutRequestSecrets()
    {
        var environment = new TestEnvironment();
        using var log = new LoggerConfiguration().ConfigureErrorLogging(Configuration(environment.ContentRootPath), environment).CreateLogger();
        using var factory = LoggerFactory.Create(builder => builder.ClearProviders().AddSerilog(log, dispose: false));
        var context = new DefaultHttpContext { TraceIdentifier = "pdf-error-reference-01" };
        context.Request.Method = "GET";
        context.Request.Path = "/InvoiceDocuments/Pdf/test-invoice";
        context.Request.QueryString = new("?token=private-query-value");
        context.Request.Headers.Authorization = "Bearer private-header-value";
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new DependencyUnavailableException(
            "InvoicePdfUnavailable", new InvalidOperationException("Injected browser initialization failure")),
            factory.CreateLogger<ExceptionHandlingMiddleware>());
        await middleware.InvokeAsync(context);
        Assert.Equal(503, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var response = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains(context.TraceIdentifier, response);
        Assert.DoesNotContain("Injected browser", response);
        var text = await ReadLogs(environment.ContentRootPath);
        Assert.Contains(context.TraceIdentifier, text);
        Assert.Contains("Injected browser initialization failure", text);
        Assert.Contains("/InvoiceDocuments/Pdf/test-invoice", text);
        Assert.Contains("503", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.DoesNotContain("private-query-value", text);
        Assert.DoesNotContain("private-header-value", text);
    }

    [Fact]
    public async Task MvcValidationAndHtmlErrorsExposeTheReferenceWrittenToTheLog()
    {
        var environment = new TestEnvironment();
        await using var app = await WorkspaceUiTests.StartAsync(configureServices: services =>
            services.AddSerilog((_, logger) => logger.ConfigureErrorLogging(Configuration(environment.ContentRootPath), environment)));
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        var form = await client.GetStringAsync("/Invoices/Create?culture=ar");
        using var result = await WorkspaceUiTests.Post(client, "/Invoices/Create?culture=ar", WorkspaceUiTests.Token(form),
            ("OperationId", Guid.NewGuid().ToString()), ("CustomerName", ""));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        var json = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        var reference = json.RootElement.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(reference));
        Assert.Equal(reference, result.Headers.GetValues("X-Request-Id").Single());
        var text = await ReadLogs(environment.ContentRootPath);
        Assert.Contains(reference, text);
        Assert.Contains("CustomerName", text);
        Assert.Contains("400", text);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/InvoiceDocuments/Pdf/{Guid.NewGuid()}?culture=ar");
        request.Headers.Accept.ParseAdd("text/html");
        using var html = await client.SendAsync(request);
        var htmlReference = html.Headers.GetValues("X-Request-Id").Single();
        var page = System.Net.WebUtility.HtmlDecode(await html.Content.ReadAsStringAsync());
        Assert.Contains("رقم الخطأ", page);
        Assert.Contains(htmlReference, page);
        Assert.Contains(htmlReference, await ReadLogs(environment.ContentRootPath));
    }

    [Fact]
    public async Task ClientCancellationIsNotRecordedAsAnApplicationError()
    {
        var environment = new TestEnvironment();
        using var log = new LoggerConfiguration().ConfigureErrorLogging(Configuration(environment.ContentRootPath), environment).CreateLogger();
        using var factory = LoggerFactory.Create(builder => builder.ClearProviders().AddSerilog(log, dispose: false));
        using var source = new CancellationTokenSource();
        source.Cancel();
        var context = new DefaultHttpContext { RequestAborted = source.Token, TraceIdentifier = "cancelled-reference" };
        var middleware = new ExceptionHandlingMiddleware(_ => throw new OperationCanceledException(source.Token),
            factory.CreateLogger<ExceptionHandlingMiddleware>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
        Assert.DoesNotContain("cancelled-reference", await ReadLogs(environment.ContentRootPath));
    }

    private static IConfiguration Configuration(string root) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Logging:File:Directory"] = Path.Combine(root, "Logs") }).Build();

    private static async Task<string> ReadLogs(string root)
    {
        var files = Directory.GetFiles(Path.Combine(root, "Logs"), "errors-*.log");
        return string.Join("\n", await Task.WhenAll(files.Select(async path =>
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        })));
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "NEXT.Tests";
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "next-error-log-" + Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
