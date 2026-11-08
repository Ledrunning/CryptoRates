using CryptocurrencyRates.Services.Services;
using Microsoft.AspNetCore.SignalR;

namespace CryptoRates.Gateway.Hubs;

/// <summary>
///     SignalR hub at <c>/hubs/rates</c>. Clients listen for the "RatesUpdated" message.
/// </summary>
public sealed class RatesHub(RatesCache cache) : Hub
{
    public const string RatesUpdatedMethod = "RatesUpdated";

    public override async Task OnConnectedAsync()
    {
        // A new client gets the current data immediately instead of waiting for the next poll.
        if (cache.Snapshot is { } snapshot)
        {
            await Clients.Caller.SendAsync(RatesUpdatedMethod, snapshot);
        }

        await base.OnConnectedAsync();
    }
}