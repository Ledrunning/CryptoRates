using System.Text.Json;

namespace CryptoRates.Tests.Services.TestHelpers;

internal sealed record Coin(
    string? Id,
    decimal? Price = 1m,
    int? Rank = null,
    string? Symbol = null,
    string? Name = null,
    DateTimeOffset? LastUpdated = null);

/// <summary>Builds CoinGecko /coins/markets response bodies.</summary>
internal static class MarketsJson
{
    public static string Of(params Coin[] coins) => OfMany(coins);

    public static string OfMany(IEnumerable<Coin> coins) =>
        JsonSerializer.Serialize(coins.Select(c =>
        {
            var item = new Dictionary<string, object?>
            {
                ["id"] = c.Id,
                ["current_price"] = c.Price,
                ["market_cap_rank"] = c.Rank
            };

            if (c.Symbol is not null) item["symbol"] = c.Symbol;
            if (c.Name is not null) item["name"] = c.Name;
            if (c.LastUpdated is not null) item["last_updated"] = c.LastUpdated;
            return item;
        }));

    /// <summary>A page of <paramref name="count"/> coins named coin{rank}, ranked from <paramref name="firstRank"/>.</summary>
    public static string Page(int count, int firstRank = 1) =>
        OfMany(Enumerable.Range(firstRank, count).Select(rank => new Coin($"coin{rank}", rank, rank)));
}
