using CryptocurrencyRates.Gateway.Models;

namespace CryptocurrencyRates.Services.Contracts;

/// <summary>
///     Pushes a fresh snapshot to connected clients.
/// </summary>
public interface IRatesNotifier
{
    Task NotifyAsync(RatesSnapshot snapshot, CancellationToken cancellationToken);
}