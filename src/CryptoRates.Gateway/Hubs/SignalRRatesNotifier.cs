using CryptocurrencyRates.Gateway.Models;
using CryptocurrencyRates.Services.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace CryptoRates.Gateway.Hubs;

public sealed class SignalRRatesNotifier(IHubContext<RatesHub> hubContext) : IRatesNotifier
{
    public Task NotifyAsync(RatesSnapshot snapshot, CancellationToken cancellationToken)
    {
        return hubContext.Clients.All.SendAsync(RatesHub.RatesUpdatedMethod, snapshot, cancellationToken);
    }
}