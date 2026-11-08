using CryptocurrencyRates.Services.Models;

namespace CryptocurrencyRates.Services.Contracts;

/// <summary>
///     Source of cryptocurrency rates. Implement this to plug in another data provider.
/// </summary>
public interface IRatesProvider
{
    /// <summary>
    ///     Fetches current USD rates for the coins configured in <c>Rates:Coins</c>.
    /// </summary>
    /// <exception cref="HttpRequestException">The provider is unreachable or returned an error status.</exception>
    /// <exception cref="InvalidOperationException">The provider returned no usable data.</exception>
    Task<IReadOnlyList<CryptoRate>> GetRatesAsync(CancellationToken cancellationToken);
}