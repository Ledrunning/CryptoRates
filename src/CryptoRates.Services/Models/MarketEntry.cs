using System.Text.Json.Serialization;

namespace CryptoRates.Services.Models
{
    internal sealed record MarketEntry(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("symbol")] string? Symbol,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("market_cap_rank")] int? MarketCapRank,
        [property: JsonPropertyName("current_price")] decimal? CurrentPrice,
        [property: JsonPropertyName("last_updated")] DateTimeOffset? LastUpdated);
}
