using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CryptoRates.Services.Tests;

public class RatesCacheTests
{
    [Fact]
    public void Snapshot_Initially_Null()
    {
        var cache = new RatesCache();
        Assert.Null(cache.Snapshot);
    }

    [Fact]
    public void Update_ThenSnapshot_ReturnsSameInstance()
    {
        var cache = new RatesCache();
        var snapshot = SnapshotBuilder.Snapshot(SnapshotBuilder.Rate("bitcoin", rank: 1));

        cache.Update(snapshot);

        Assert.Same(snapshot, cache.Snapshot);
    }

    [Fact]
    public void Update_Null_ThrowsArgumentNullException()
    {
        var cache = new RatesCache();
        Assert.Throws<ArgumentNullException>(() => cache.Update(null!));
    }

    [Fact]
    public void ParallelReadersAndWriter_NeverSeeNullAfterFirstWrite()
    {
        var cache = new RatesCache();
        cache.Update(SnapshotBuilder.Snapshot(SnapshotBuilder.Rate("bitcoin", rank: 1))); // первая запись

        var nullObservations = new ConcurrentBag<int>();

        Parallel.For(0, 500, i =>
        {
            if (cache.Snapshot is null)
            {
                nullObservations.Add(i);
            }

            if (i % 2 == 0)
            {
                cache.Update(SnapshotBuilder.Rate($"coin-{i}", rank: i));
            }
        });

        // После первой записи читатели никогда не видят null, даже параллельно с писателями.
        Assert.Empty(nullObservations);
    }
}