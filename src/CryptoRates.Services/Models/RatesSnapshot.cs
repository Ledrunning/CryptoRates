using CryptocurrencyRates.Services.Models;

namespace CryptocurrencyRates.Gateway.Models;

/// <summary>
///     The latest set of rates together with the moment the gateway fetched it.
/// </summary>
public sealed record RatesSnapshot(IReadOnlyList<CryptoRate> Rates, DateTimeOffset FetchedAt);