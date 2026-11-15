namespace CryptoRates.Services.Tests.TestHelpers;

/// <summary>
///     Построитель JSON-ответа провайдера на <c>/coins/markets</c> (формат CoinGecko MarketEntry).
/// </summary>
public static class MarketsResponseBuilder
{
    public static string Coin(
        string id,
        string? symbol = null,
        string? name = null,
        decimal? currentPrice = null,
        int? marketCapRank = null,
        string? lastUpdated = null)
    {
        var dict = new Dictionary<string, object?> { ["id"] = id };
        if (symbol is not null)
        {
            dict["symbol"] = symbol;
        }

        if (name is not null)
        {
            dict["name"] = name;
        }

        if (currentPrice is { } price)
        {
            dict["current_price"] = price;
        }

        if (marketCapRank is { } rank)
        {
            dict["market_cap_rank"] = rank;
        }

        if (lastUpdated is not null)
        {
            dict["last_updated"] = lastUpdated;
        }

        return JsonSerializer.Serialize(dict);
    }

    public static string Array(params string[] coins) => "[" + string.Join(",", coins) + "]";

    /// <summary>
    ///     N монет: <c>coin-1</c> .. <c>coin-N</c>, цена убывает, ранги по порядку.
    /// </summary>
    public static string Series(int count, int firstId = 1)
    {
        var coins = new string[count];
        for (var i = 0; i < count; i++)
        {
            var id = $"coin-{firstId + i}";
            coins[i] = Coin(id, symbol: id, name: id, currentPrice: count - i, marketCapRank: firstId + i);
        }

        return Array(coins);
    }
}
