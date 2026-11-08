using NEXT.Infrastructure.Identity.Security.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class SecurityEmailWorker(IServiceScopeFactory scopes, IOptions<SmtpEmailOptions> smtp,
    IOptions<ResendEmailOptions> api, ILogger<SecurityEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var deliveryEnabled = !string.IsNullOrWhiteSpace(api.Value.ApiKey) || !string.IsNullOrWhiteSpace(smtp.Value.Host);
        if (!deliveryEnabled)
        {
            logger.LogWarning("Security email delivery is disabled until Email:Resend or Email:Smtp is configured.");
        }
        var nextCleanup = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (DateTimeOffset.UtcNow >= nextCleanup)
                {
                    await scope.ServiceProvider.GetRequiredService<SecurityMaintenance>().CleanupAsync(stoppingToken);
                    nextCleanup = DateTimeOffset.UtcNow.AddMinutes(1);
                }
                var dispatched = deliveryEnabled && await scope.ServiceProvider.GetRequiredService<SecurityEmailDispatcher>()
                    .DispatchOneAsync(stoppingToken);
                if (!dispatched) await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch
            {
                logger.LogWarning("Security email dispatch failed; a retry will follow.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
