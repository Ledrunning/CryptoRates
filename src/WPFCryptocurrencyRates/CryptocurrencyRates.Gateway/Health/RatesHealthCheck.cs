using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CryptocurrencyRates.Gateway.Health;

/// <summary>
///     Unhealthy until the first successful poll, degraded when data is older than three poll intervals.
/// </summary>
public sealed class RatesHealthCheck(
    RatesCache cache,
    IOptions<RatesOptions> options,
    TimeProvider timeProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (cache.Snapshot is not { } snapshot)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("No rates have been loaded yet."));
        }

        var age = timeProvider.GetUtcNow() - snapshot.FetchedAt;
        var maxAge = options.Value.PollInterval * 3;

        return Task.FromResult(age > maxAge
            ? HealthCheckResult.Degraded($"Rates are stale (last update {age:g} ago).")
            : HealthCheckResult.Healthy());
    }
}
