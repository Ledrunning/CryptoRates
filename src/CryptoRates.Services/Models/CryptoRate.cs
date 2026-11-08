namespace CryptocurrencyRates.Services.Models;

/// <summary>
///     Gateway's own rate model. It does not depend on the shape of any external provider.
/// </summary>
public sealed record CryptoRate(
    string Id,
    string Symbol,
    string Name,
    int? Rank,
    decimal PriceUsd,
    DateTimeOffset UpdatedAt);