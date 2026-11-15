using CryptocurrencyRates.Gateway.Services;
using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Contracts;
using CryptocurrencyRates.Services.Services;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace CryptoRates.Services.Tests;

public class RatesPollingServiceTests
{
    private static RatesPollingService CreateHost(
        IRatesProvider provider,
        RatesCache cache,
        IRatesNotifier notifier,
        FakeTimeProvider time,
        RatesOptions? options = null)
    {
        options ??= new RatesOptions { PollInterval = TimeSpan.FromMinutes(10) };
        var services = new ServiceCollection();
        services.AddSingleton(provider);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new RatesPollingService(scopeFactory, cache, notifier, Options.Create(options), time,
            new FakeLogger<RatesPollingService>());
    }

    private static IReadOnlyList<CryptoRate> TwoRates() =>
        new List<CryptoRate>
        {
            SnapshotBuilder.Rate("bitcoin", rank: 1, priceUsd: 50000m),
            SnapshotBuilder.Rate("ethereum", rank: 2, priceUsd: 3000m)
        };

    [Fact]
    public async Task PollOnceAsync_Success_UpdatesCacheAndNotifiesOnceWithSnapshotFromTimeProvider()
    {
        // Arrange
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var provider = Substitute.For<IRatesProvider>();
        provider.GetRatesAsync(Arg.Any<CancellationToken>()).Returns(TwoRates());
        var notifier = Substitute.For<IRatesNotifier>();
        var cache = new RatesCache();
        var host = CreateHost(provider, cache, notifier, time);

        // Act
        await host.PollOnceAsync(CancellationToken.None);

        // Assert
        var snapshot = Assert.NotNull(cache.Snapshot);
        Assert.Equal(time.GetUtcNow(), snapshot.FetchedAt);
        Assert.Equal(2, snapshot.Rates.Count);
        Assert.Equal("bitcoin", snapshot.Rates[0].Id);
        await notifier.Received(1).NotifyAsync(snapshot, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnceAsync_SecondPoll_FetchedAtAdvancesWithTimeProvider()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var provider = Substitute.For<IRatesProvider>();
        provider.GetRatesAsync(Arg.Any<CancellationToken>()).Returns(TwoRates());
        var notifier = Substitute.For<IRatesNotifier>();
        var cache = new RatesCache();
        var host = CreateHost(provider, cache, notifier, time);

        // Act
        await host.PollOnceAsync(CancellationToken.None);
        var first = cache.Snapshot!.FetchedAt;

        time.Advance(TimeSpan.FromMinutes(5));
        await host.PollOnceAsync(CancellationToken.None);
        var second = cache.Snapshot!.FetchedAt;

        // Assert
        Assert.True(second > first);
        Assert.Equal(time.GetUtcNow(), second);
    }

    [Fact]
    public async Task PollOnceAsync_ProviderFails_KeepsPreviousSnapshot_NotifierSilent_NextPollWorks()
    {
        // Arrange
        var provider = Substitute.For<IRatesProvider>();
        provider.GetRatesAsync(Arg.Any<CancellationToken>()).Returns(TwoRates());
        var notifier = Substitute.For<IRatesNotifier>();
        var cache = new RatesCache();
        var host = CreateHost(provider, cache, notifier, new FakeTimeProvider());

        await host.PollOnceAsync(CancellationToken.None);
        var first = cache.Snapshot!;

        // Act: провайдер падает
        provider.GetRatesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new HttpRequestException("provider down"));
        await host.PollOnceAsync(CancellationToken.None);

        // Assert: кэш не изменился, нотизатор не вызывался повторно
        Assert.Same(first, cache.Snapshot);
        await notifier.Received(1).NotifyAsync(Arg.Any<RatesSnapshot>(), Arg.Any<CancellationToken>());

        // Act: следующий тик работает снова
        provider.GetRatesAsync(Arg.Any<CancellationToken>()).Returns(TwoRates());
        await host.PollOnceAsync(CancellationToken.None);

        Assert.NotSame(first, cache.Snapshot);
        Assert.Equal(2, cache.Snapshot!.Rates.Count);
        await notifier.Received(2).NotifyAsync(Arg.Any<RatesSnapshot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnceAsync_NotifierFails_CacheStillUpdated_NextPollsWork()
    {
        // Arrange
        var provider = Substitute.For<IRatesProvider>();
        provider.GetRatesAsync(Arg.Any<CancellationToken>()).Returns(TwoRates());
        var notifier = Substitute.For<IRatesNotifier>();
        notifier.NotifyAsync(Arg.Any<RatesSnapshot>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("notifier down"));
        var cache = new RatesCache();
        var host = CreateHost(provider, cache, notifier, new FakeTimeProvider());

        // Act
        await host.PollOnceAsync(CancellationToken.None);

        // Assert: кэш обновился несмотря на сбой нотификатора
        Assert.Equal(2, cache.Snapshot!.Rates.Count);

        // Act: следующий тик работает
        await host.PollOnceAsync(CancellationToken.None);
        Assert.Equal(2, cache.Snapshot!.Rates.Count);
        await notifier.Received(2).NotifyAsync(Arg.Any<RatesSnapshot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnceAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var provider = Substitute.For<IRatesProvider>();
        provider.GetRatesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => throw new OperationCanceledException());
        var host = CreateHost(provider, new RatesCache(), Substitute.For<IRatesNotifier>(), new FakeTimeProvider());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => host.PollOnceAsync(cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_Cancellation_CompletesWithoutException()
    {
        // Arrange
        var provider = Substitute.For<IRatesProvider>();
        provider.GetRatesAsync(Arg.Any<CancellationToken>()).Returns(TwoRates());
        var notifier = Substitute.For<IRatesNotifier>();
        var cache = new RatesCache();
        var time = new FakeTimeProvider();
        var host = CreateHost(provider, cache, notifier, time);
        var cts = new CancellationTokenSource();

        var task = host.ExecuteAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert: завершается без исключения
        await task;
    }
}