using CryptocurrencyRates.Gateway.Models;
using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Models;
using CryptocurrencyRates.Services.Services;
using CryptoRates.Gateway.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CryptoRates.Tests.Gateway;

public class RatesHealthCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private static (RatesHealthCheck Check, RatesCache Cache) Create()
    {
        var cache = new RatesCache();
        var options = Options.Create(new RatesOptions { PollInterval = Interval });
        return (new RatesHealthCheck(cache, options, new FakeTimeProvider(Now)), cache);
    }

    private static RatesSnapshot SnapshotAged(TimeSpan age) =>
        new(new[] { new CryptoRate("bitcoin", "BTC", "Bitcoin", 1, 100m, Now) }, Now - age);

    private static Task<HealthCheckResult> Run(RatesHealthCheck check) =>
        check.CheckHealthAsync(new HealthCheckContext());

    [Fact]
    public async Task CheckHealthAsync_NothingLoadedYet_IsUnhealthy()
    {
        var (check, _) = Create();

        var result = await Run(check);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_FreshSnapshot_IsHealthy()
    {
        var (check, cache) = Create();
        cache.Update(SnapshotAged(TimeSpan.FromMinutes(1)));

        var result = await Run(check);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ExactlyThreeIntervalsOld_IsStillHealthy()
    {
        var (check, cache) = Create();
        cache.Update(SnapshotAged(Interval * 3));

        var result = await Run(check);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_OlderThanThreeIntervals_IsDegraded()
    {
        var (check, cache) = Create();
        cache.Update(SnapshotAged(Interval * 3 + TimeSpan.FromSeconds(1)));

        var result = await Run(check);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}
