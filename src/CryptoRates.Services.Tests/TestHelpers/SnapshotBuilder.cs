using CryptocurrencyRates.Services.Models;

namespace CryptoRates.Services.Tests.TestHelpers;

/// <summary>
///     Построители записей курсов и снимков для тестов.
/// </summary>
public static class SnapshotBuilder
{
    public static CryptoRate Rate(
        string id = "bitcoin",
        string? symbol = null,
        string? name = null,
        int? rank = null,
        decimal priceUsd = 1m,
        DateTimeOffset? updatedAt = null)
    {
        return new CryptoRate(id, symbol ?? id.ToUpperInvariant(), name ?? id, rank, priceUsd, updatedAt ?? DateTimeOffset.UtcNow);
    }

    public static RatesSnapshot Snapshot(params CryptoRate[] rates)
    {
        return new RatesSnapshot(rates, DateTimeOffset.UtcNow);
    }
}
