using CryptoRates.Client.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoRates.Client.Contracts;

public enum StreamState
{ Connecting, Connected, Reconnecting }

public interface IRatesStream : IAsyncDisposable
{
    event Action<RatesSnapshotModel>? SnapshotReceived;

    event Action<StreamState>? StateChanged;

    /// <summary>Connects and keeps retrying until the gateway is up or the token is cancelled.</summary>
    Task StartAsync(CancellationToken token);

    Task StopAsync();
}