using CryptocurrencyRates.Gateway.Models;
using CryptocurrencyRates.Services.Models;
using CryptocurrencyRates.Services.Services;
using Xunit;

namespace CryptoRates.Tests.Services;

public class RatesCacheTests
{
    private static RatesSnapshot Snapshot(int seed = 0) =>
        new([new CryptoRate("bitcoin", "BTC", "Bitcoin", 1, 100m + seed, DateTimeOffset.UnixEpoch)],
            DateTimeOffset.UnixEpoch.AddSeconds(seed));

    [Fact]
    public void Snapshot_Initially_IsNull()
    {
        Assert.Null(new RatesCache().Snapshot);
    }

    [Fact]
    public void Update_StoresTheSnapshot()
    {
        var cache = new RatesCache();
        var snapshot = Snapshot();

        cache.Update(snapshot);

        Assert.Same(snapshot, cache.Snapshot);
    }

    [Fact]
    public void Update_Twice_KeepsTheLatest()
    {
        var cache = new RatesCache();
        var second = Snapshot(2);

        cache.Update(Snapshot(1));
        cache.Update(second);

        Assert.Same(second, cache.Snapshot);
    }

    [Fact]
    public void Update_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RatesCache().Update(null!));
    }

    [Fact]
    public void Update_ConcurrentWithReads_NeverExposesNullAfterFirstWrite()
    {
        var cache = new RatesCache();
        cache.Update(Snapshot());
        var nulls = 0;

        Parallel.For(1, 2000, i =>
        {
            cache.Update(Snapshot(i));
            if (cache.Snapshot is null)
            {
                Interlocked.Increment(ref nulls);
            }
        });

        Assert.Equal(0, nulls);
    }
}
