using CryptocurrencyRates.Gateway.Models;

namespace CryptocurrencyRates.Services.Services;

/// <summary>
///     Holds the latest snapshot in memory. Clients read from here, never from the external API.
/// </summary>
public sealed class RatesCache
{
    private RatesSnapshot? snapshot;

    public RatesSnapshot? Snapshot => Volatile.Read(ref snapshot);

    public void Update(RatesSnapshot newSnapshot)
    {
        ArgumentNullException.ThrowIfNull(newSnapshot);
        Volatile.Write(ref snapshot, newSnapshot);
    }
}