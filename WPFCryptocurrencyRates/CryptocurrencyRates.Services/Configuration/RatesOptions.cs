namespace CryptocurrencyRates.Services.Configuration;

public sealed class RatesOptions
{
    public const string SectionName = "Rates";

    /// <summary>Base URL of the rates API. Must be absolute.</summary>
    public Uri BaseUrl { get; set; } = new("https://api.coingecko.com/api/v3/");

    /// <summary>Optional API key. Keep it out of git: use user-secrets or environment variables.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    ///     How often the gateway asks the provider for fresh rates.
    ///     Mind the provider's monthly quota: 5 minutes is about 8,600 calls per month.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Coins to track.</summary>
    public List<CoinOption> Coins { get; set; } = [];
}

public sealed class CoinOption
{
    /// <summary>Provider's coin id, e.g. "bitcoin".</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Ticker symbol shown to clients, e.g. "BTC".</summary>
    public string Symbol { get; set; } = string.Empty;
}
