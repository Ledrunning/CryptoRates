using CryptocurrencyRates.Common.Exceptions;
using CryptoCurrencyRates.Client.Contracts;
using CryptoRates.Client.Model;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoRates.Client.Services.Rest;

public sealed class CryptoCurrencyRatesService(HttpClient httpClient) : ICryptoCurrencyRatesService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict
    };

    /// <inheritdoc cref="ICryptoCurrencyRatesService" />
    public async Task<List<CurrencyRateModel>?> GetAllCryptoCurrencyRatesAsync(CancellationToken token)
    {
        try
        {
            // Empty relative URI means "the BaseAddress itself" (the full gateway URL).
            using var response = await httpClient.GetAsync(string.Empty, token);

            if (!response.IsSuccessStatusCode)
            {
                throw new CryptoCurrencyRatesException(
                    $"CryptoRates.Gateway responded with {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            var snapshot = await JsonSerializer.DeserializeAsync<RatesSnapshotModel>(stream, JsonOptions, token)
                           ?? throw new CryptoCurrencyRatesException("CryptoRates.Gateway returned an empty response.");

            return snapshot.Rates;
        }
        catch (HttpRequestException ex)
        {
            throw new CryptoCurrencyRatesException("CryptoRates.Gateway is unreachable.", ex);
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
        {
            // Not our cancellation: the HttpClient timeout fired.
            throw new CryptoCurrencyRatesException("CryptoRates.Gateway did not respond in time.", ex);
        }
        catch (JsonException ex)
        {
            throw new CryptoCurrencyRatesException("CryptoRates.Gateway returned an unexpected response.", ex);
        }
    }
}