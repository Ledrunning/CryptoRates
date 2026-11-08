using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Contracts;
using CryptocurrencyRates.Services.Models;
using Microsoft.Extensions.Options;

namespace CryptocurrencyRates.Services.Providers;

/// <summary>
///     Reads USD prices from CoinGecko's <c>/simple/price</c> endpoint in a single request.
/// </summary>
public sealed class CoinGeckoRatesProvider(
    HttpClient httpClient,
    IOptions<RatesOptions> options,
    TimeProvider timeProvider) : IRatesProvider
{
    // CoinGecko's header for the free Demo plan. Verify it against the current CoinGecko docs.
    public const string ApiKeyHeader = "x-cg-demo-api-key";

    public async Task<IReadOnlyList<CryptoRate>> GetRatesAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var ids = string.Join(',', settings.Coins.Select(c => c.Id));
        var relativeUrl = $"simple/price?ids={Uri.EscapeDataString(ids)}&vs_currencies=usd&include_last_updated_at=true";

        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Add(ApiKeyHeader, settings.ApiKey);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, PriceEntry>>(cancellationToken)
                      ?? throw new InvalidOperationException("The rates provider returned an empty response.");

        var rates = new List<CryptoRate>(settings.Coins.Count);
        foreach (var coin in settings.Coins)
        {
            if (!payload.TryGetValue(coin.Id, out var entry) || entry.Usd is not { } price)
            {
                continue; // coin missing in the response: skip it, the rest is still useful
            }

            var updatedAt = entry.LastUpdatedAt is { } unixSeconds
                ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                : timeProvider.GetUtcNow();

            rates.Add(new CryptoRate(coin.Id, coin.Symbol, price, updatedAt));
        }

        if (rates.Count == 0)
        {
            throw new InvalidOperationException("The rates provider returned no usable rates.");
        }

        return rates;
    }

    private sealed record PriceEntry(
        [property: JsonPropertyName("usd")] decimal? Usd,
        [property: JsonPropertyName("last_updated_at")] long? LastUpdatedAt);
}
