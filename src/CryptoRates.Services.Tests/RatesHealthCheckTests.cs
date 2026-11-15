using CryptocurrencyRates.Gateway.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CryptoRates.Services.Tests;

public class RatesHealthCheckTests
{
    [Fact]
    public async Task CheckHealth_FreshSnapshot_Healthy()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        time.SetUtcNow(now);
        var cache = new RatesCache();
        cache.Update(SnapshotBuilder.Snapshot(SnapshotBuilder.Rate("bitcoin", rank: 1)));
        var options = Options.Create(new RatesOptions { PollInterval = TimeSpan.FromMinutes(10) });
        var check = new RatesHealthCheck(cache, options, time);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealth_EmptyCache_Unhealthy()
    {
        // Arrange
        var cache = new RatesCache();
        var options = Options.Create(new RatesOptions { PollInterval = TimeSpan.FromMinutes(10) });
        var check = new RatesHealthCheck(cache, options, new FakeTimeProvider());

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealth_StaleSnapshot_Degraded()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var fetchedAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        time.SetUtcNow(fetchedAt);
        var cache = new RatesCache();
        cache.Update(SnapshotBuilder.Snapshot(SnapshotBuilder.Rate("bitcoin", rank: 1)));
        var options = Options.Create(new RatesOptions { PollInterval = TimeSpan.FromMinutes(1) });
        var check = new RatesHealthCheck(cache, options, time);

        // Act: данные устарели больше чем на 3 интервала опроса
        time.Advance(TimeSpan.FromMinutes(4));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealth_ExactlyMaxAge_Healthy()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var fetchedAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        time.SetUtcNow(fetchedAt);
        var cache = new RatesCache();
        cache.Update(SnapshotBuilder.Snapshot(SnapshotBuilder.Rate("bitcoin", rank: 1)));
        var options = Options.Create(new RatesOptions { PollInterval = TimeSpan.FromMinutes(1) });
        var check = new RatesHealthCheck(cache, options, time);

        // Act: возраст ровно 3 интервала — не считается устаревшим (проверка «>», не «>=»)
        time.Advance(TimeSpan.FromMinutes(3));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}