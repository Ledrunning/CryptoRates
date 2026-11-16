using CryptocurrencyRates.Gateway.Models;
using CryptocurrencyRates.Gateway.Services;
using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Contracts;
using CryptocurrencyRates.Services.Models;
using CryptocurrencyRates.Services.Services;
using CryptoRates.Tests.Services.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CryptoRates.Services.Tests;

public class RatesPollingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<CryptoRate> Rates =
        new[] { new CryptoRate("bitcoin", "BTC", "Bitcoin", 1, 100m, Now) };

    private sealed class StubProvider(Func<CancellationToken, Task<IReadOnlyList<CryptoRate>>> fetch) : IRatesProvider
    {
        public int Calls;

        public Task<IReadOnlyList<CryptoRate>> GetRatesAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return fetch(cancellationToken);
        }
    }

    private sealed class RecordingNotifier(Func<RatesSnapshot, Task>? onNotify = null) : IRatesNotifier
    {
        private readonly List<RatesSnapshot> snapshots = [];

        public IReadOnlyList<RatesSnapshot> Snapshots
        {
            get
            {
                lock (snapshots)
                {
                    return snapshots.ToArray();
                }
            }
        }

        public Task NotifyAsync(RatesSnapshot snapshot, CancellationToken cancellationToken)
        {
            lock (snapshots)
            {
                snapshots.Add(snapshot);
            }

            return onNotify?.Invoke(snapshot) ?? Task.CompletedTask;
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider services;

        public Harness(
            Func<CancellationToken, Task<IReadOnlyList<CryptoRate>>>? fetch = null,
            Func<RatesSnapshot, Task>? onNotify = null)
        {
            Provider = new StubProvider(fetch ?? (_ => Task.FromResult(Rates)));
            Notifier = new RecordingNotifier(onNotify);
            Time = new FakeTimeProvider(Now);
            Logger = new TestLogger<RatesPollingService>();
            Settings = new RatesOptions { PollInterval = TimeSpan.FromMinutes(10) };

            services = new ServiceCollection().AddSingleton<IRatesProvider>(Provider).BuildServiceProvider();
            Service = new RatesPollingService(
                services.GetRequiredService<IServiceScopeFactory>(),
                Cache,
                Notifier,
                Options.Create(Settings),
                Time,
                Logger);
        }

        public StubProvider Provider { get; }
        public RecordingNotifier Notifier { get; }
        public RatesCache Cache { get; } = new();
        public FakeTimeProvider Time { get; }
        public TestLogger<RatesPollingService> Logger { get; }
        public RatesOptions Settings { get; }
        public RatesPollingService Service { get; }

        public void Dispose()
        {
            Service.Dispose();
            services.Dispose();
        }
    }

    [Fact]
    public async Task PollOnceAsync_Success_UpdatesCacheAndNotifiesOnce()
    {
        using var h = new Harness();

        await h.Service.PollOnceAsync(CancellationToken.None);

        var cached = Assert.IsType<RatesSnapshot>(h.Cache.Snapshot);
        Assert.Equal(Rates, cached.Rates);
        Assert.Equal(Now, cached.FetchedAt); // stamped by the gateway clock
        Assert.Same(cached, Assert.Single(h.Notifier.Snapshots));
    }

    [Fact]
    public async Task PollOnceAsync_ProviderFails_KeepsPreviousSnapshotAndDoesNotNotify()
    {
        using var h = new Harness(_ => throw new HttpRequestException("boom"));
        var previous = new RatesSnapshot(Rates, Now.AddMinutes(-10));
        h.Cache.Update(previous);

        await h.Service.PollOnceAsync(CancellationToken.None); // must not throw

        Assert.Same(previous, h.Cache.Snapshot);
        Assert.Empty(h.Notifier.Snapshots);
        Assert.Contains(h.Logger.Warnings, e => e.Exception is HttpRequestException);
    }

    [Fact]
    public async Task PollOnceAsync_ProviderFailsOnFirstPoll_CacheStaysEmpty()
    {
        using var h = new Harness(_ => throw new HttpRequestException("boom"));

        await h.Service.PollOnceAsync(CancellationToken.None);

        Assert.Null(h.Cache.Snapshot);
    }

    [Fact]
    public async Task PollOnceAsync_NotifierFails_CacheIsStillUpdated()
    {
        using var h = new Harness(onNotify: _ => throw new InvalidOperationException("hub is down"));

        await h.Service.PollOnceAsync(CancellationToken.None); // must not throw

        Assert.NotNull(h.Cache.Snapshot);
        Assert.Contains(h.Logger.Warnings, e => e.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task PollOnceAsync_CancelledByCaller_Throws()
    {
        using var h = new Harness(ct =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Rates);
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Service.PollOnceAsync(cts.Token));

        Assert.Null(h.Cache.Snapshot);
    }

    [Fact]
    public async Task PollOnceAsync_TimeoutNotCausedByCaller_IsTreatedAsFailure()
    {
        // An HttpClient timeout surfaces as a cancellation, but the caller did not cancel anything.
        using var h = new Harness(_ => throw new OperationCanceledException());

        await h.Service.PollOnceAsync(CancellationToken.None); // must not throw

        Assert.Null(h.Cache.Snapshot);
        Assert.NotEmpty(h.Logger.Warnings);
    }

    [Fact]
    public async Task StartAsync_PollsImmediatelyAndOnEveryInterval()
    {
        using var h = new Harness();

        await h.Service.StartAsync(CancellationToken.None);
        await TestWait.UntilAsync(() => h.Notifier.Snapshots.Count == 1, "the first poll");

        h.Time.Advance(h.Settings.PollInterval);
        await TestWait.UntilAsync(() => h.Notifier.Snapshots.Count == 2, "the second poll");

        h.Time.Advance(h.Settings.PollInterval);
        await TestWait.UntilAsync(() => h.Notifier.Snapshots.Count == 3, "the third poll");

        await h.Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_EndsTheLoopWithoutErrors()
    {
        using var h = new Harness();
        await h.Service.StartAsync(CancellationToken.None);
        await TestWait.UntilAsync(() => h.Notifier.Snapshots.Count == 1, "the first poll");

        await h.Service.StopAsync(CancellationToken.None);

        Assert.True(h.Service.ExecuteTask?.IsCompletedSuccessfully ?? true);
    }

    [Fact]
    public async Task StartAsync_FailingProvider_LoopKeepsRunning()
    {
        var attempt = 0;
        using var h = new Harness(_ =>
        {
            attempt++;
            return attempt == 1
                ? throw new HttpRequestException("first poll fails")
                : Task.FromResult(Rates);
        });

        await h.Service.StartAsync(CancellationToken.None);
        await TestWait.UntilAsync(() => h.Provider.Calls == 1, "the failing poll");

        h.Time.Advance(h.Settings.PollInterval);
        await TestWait.UntilAsync(() => h.Cache.Snapshot is not null, "recovery on the next tick");

        await h.Service.StopAsync(CancellationToken.None);
    }
}
