namespace CryptocurrencyRates.Services.Configuration;

public sealed class RatesOptions
{
    public const string SectionName = "Rates";
    public const int PageSize = 250;      // максимальное значение per_page в каталоге /coins/markets
    public const int MaxPinned = 50;      // limit on the number of IDs per request

    public string BaseUrl { get; set; } = "https://api.coingecko.com/api/v3/";
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(10);
    public string? ApiKey { get; set; }

    public int MaxCoins { get; set; } = 250;
    public List<string> PinnedCoins { get; set; } = [];
    public TimeSpan PageDelay { get; set; } = TimeSpan.FromSeconds(2.5); // < 30 requests per minute
    public int MonthlyCallBudget { get; set; } = 10_000;

    public int PageCount => (MaxCoins + PageSize - 1) / PageSize;

    /// <summary>Worst case: every page plus one request for the pinned coins that are outside the top.</summary>
    public int MaxCallsPerPoll => PageCount + (PinnedCoins.Count > 0 ? 1 : 0);
}