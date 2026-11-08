using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Contracts;
using CryptocurrencyRates.Services.Models;
using CryptoRates.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace CryptocurrencyRates.Services.Providers;

/// <summary>
///     Reads USD prices from CoinGecko's <c>/simple/price</c> endpoint in a single request.
/// </summary>
public sealed class CoinGeckoRatesProvider(
    HttpClient httpClient,
    IOptions<RatesOptions> options,
    TimeProvider timeProvider,
    ILogger<CoinGeckoRatesProvider> logger) : IRatesProvider
{
    // Header for the free Demo plan.
    public const string ApiKeyHeader = "x-cg-demo-api-key";

    public async Task<IReadOnlyList<CryptoRate>> GetRatesAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var top = new List<CryptoRate>(settings.PageCount * RatesOptions.PageSize);

        for (var page = 1; page <= settings.PageCount; page++)
        {
            if (page > 1)
            {
                await Task.Delay(settings.PageDelay, timeProvider, cancellationToken);
            }

            // per_page is always 250: a different size would shift the page offsets.
            var (rates, rawCount) = await FetchAsync(
                $"coins/markets?vs_currency=usd&order=market_cap_desc&per_page={RatesOptions.PageSize}&page={page}&sparkline=false",
                cancellationToken);

            top.AddRange(rates);

            if (rawCount < RatesOptions.PageSize)
            {
                break; // the end of the list
            }
        }

        // The ranking can shift between page requests, so a coin may appear twice.
        var result = top
            .DistinctBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Take(settings.MaxCoins)
            .ToList();

        if (result.Count == 0)
        {
            throw new InvalidOperationException("The rates provider returned no usable rates.");
        }

        await AddPinnedAsync(result, cancellationToken);

        // A stable order for the clients: by market cap rank, unranked coins last.
        return result
            .OrderBy(r => r.Rank ?? int.MaxValue)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }

    private async Task AddPinnedAsync(List<CryptoRate> result, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var have = result.Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = settings.PinnedCoins
            .Where(id => !string.IsNullOrWhiteSpace(id) && !have.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count == 0)
        {
            return; // all pinned coins are already in the top
        }

        try
        {
            await Task.Delay(settings.PageDelay, timeProvider, cancellationToken);

            var ids = Uri.EscapeDataString(string.Join(',', missing));
            var (rates, _) = await FetchAsync(
                $"coins/markets?vs_currency=usd&ids={ids}&per_page={RatesOptions.PageSize}&sparkline=false",
                cancellationToken);

            result.AddRange(rates);

            var found = rates.Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in missing.Where(id => !found.Contains(id)))
            {
                logger.LogWarning("Pinned coin '{CoinId}' was not found at the provider.", id);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Pinned coins could not be loaded, publishing the top list only.");
        }
    }

    private async Task<(List<CryptoRate> Rates, int RawCount)> FetchAsync(
        string relativeUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            request.Headers.Add(ApiKeyHeader, options.Value.ApiKey);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<MarketEntry>>(cancellationToken)
                    ?? throw new InvalidOperationException("The rates provider returned an empty response.");

        var rates = new List<CryptoRate>(items.Count);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || item.CurrentPrice is not { } price)
            {
                continue;
            }

            rates.Add(new CryptoRate(
                item.Id,
                (item.Symbol ?? item.Id).ToUpperInvariant(),
                item.Name ?? item.Id,
                item.MarketCapRank,
                price,
                item.LastUpdated ?? timeProvider.GetUtcNow()));
        }

        return (rates, items.Count);
    }
}