using NEXT.Application.DTOs.Stores;
using NEXT.Infrastructure.Identity.Security.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NEXT.Infrastructure.Stores;

namespace NEXT.Infrastructure.Identity.Security.Email;

public sealed class SecurityEmailWorker(IServiceScopeFactory scopes, IOptions<SmtpEmailOptions> smtp,
    IOptions<ResendEmailOptions> api, IOptions<GmailEmailOptions> gmail, ILogger<SecurityEmailWorker> logger,
    StoreServerSettings stores, StoreScopeFactory storeScopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var deliveryEnabled = gmail.Value.IsConfigured || !string.IsNullOrWhiteSpace(api.Value.ApiKey) || !string.IsNullOrWhiteSpace(smtp.Value.Host);
        if (!deliveryEnabled)
        {
            logger.LogWarning("Security email delivery is disabled until Email:Gmail, Email:Resend or Email:Smtp is configured.");
        }
        var nextCleanup = new Dictionary<Guid, DateTimeOffset>();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var targets = new List<StoreRegistration?> { null };
                if(stores.Enabled)
                {
                    try
                    {
                        await using var catalogScope = scopes.CreateAsyncScope();
                        targets.AddRange((await catalogScope.ServiceProvider.GetRequiredService<StoreCatalog>()
                            .ListAsync(stoppingToken)).Where(s => s.State == StoreState.Active));
                    }
                    catch (OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
                    catch { logger.LogWarning("Additional stores could not be enumerated for security email processing."); }
                }
                var dispatched = false;
                foreach(var store in targets)
                {
                    try
                    {
                        await using var scope = store is null ? scopes.CreateAsyncScope() : storeScopes.Create(store);
                        var id = store?.Id ?? Guid.Empty;
                        if(!nextCleanup.TryGetValue(id,out var due) || DateTimeOffset.UtcNow >= due)
                        {
                            await scope.ServiceProvider.GetRequiredService<SecurityMaintenance>().CleanupAsync(stoppingToken);
                            nextCleanup[id] = DateTimeOffset.UtcNow.AddMinutes(1);
                        }
                        dispatched |= deliveryEnabled && await scope.ServiceProvider.GetRequiredService<SecurityEmailDispatcher>()
                            .DispatchOneAsync(stoppingToken);
                    }
                    catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { throw; }
                    catch { logger.LogWarning("Security email processing failed for store {StoreId}", store?.Id); }
                }
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
