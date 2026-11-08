using CryptocurrencyRates.Gateway.Models;
using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Contracts;
using CryptocurrencyRates.Services.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CryptocurrencyRates.Gateway.Services;

/// <summary>
///     Polls the rates provider on a timer, updates the cache and notifies connected clients.
///     If a poll fails the previous snapshot stays in the cache.
/// </summary>
public sealed class RatesPollingService(
    IServiceScopeFactory scopeFactory,
    RatesCache cache,
    IRatesNotifier notifier,
    IOptions<RatesOptions> options,
    TimeProvider timeProvider,
    ILogger<RatesPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);

        try
        {
            do
            {
                await PollOnceAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal shutdown
        }
    }

    /// <summary>
    ///     One polling cycle. Never throws except on cancellation.
    /// </summary>
    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        RatesSnapshot snapshot;

        try
        {
            // The provider is a typed HttpClient (transient), so resolve it per poll
            // instead of capturing one instance in this long-lived service.
            await using var scope = scopeFactory.CreateAsyncScope();
            var provider = scope.ServiceProvider.GetRequiredService<IRatesProvider>();

            var rates = await provider.GetRatesAsync(cancellationToken);
            snapshot = new RatesSnapshot(rates, timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh rates. Keeping the previous snapshot, if any.");
            return;
        }

        cache.Update(snapshot);
        logger.LogInformation("Rates updated: {Count} coins", snapshot.Rates.Count);

        try
        {
            await notifier.NotifyAsync(snapshot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify connected clients.");
        }
    }
}