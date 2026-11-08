using CryptoRates.Client.Contracts;
using CryptoRates.Client.Model;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoRates.Client.Services.Hub;

public sealed class RatesHubClient : IRatesStream
{
    // Must match RatesHub.RatesUpdatedMethod on the gateway.
    private const string RatesUpdatedMethod = "RatesUpdated";

    private static readonly TimeSpan FirstConnectRetryDelay = TimeSpan.FromSeconds(5);

    private readonly HubConnection connection;

    public event Action<RatesSnapshotModel>? SnapshotReceived;

    public event Action<StreamState>? StateChanged;

    public RatesHubClient(Uri hubUri)
    {
        connection = new HubConnectionBuilder()
            .WithUrl(hubUri)
            .WithAutomaticReconnect(new EndlessRetryPolicy())
            .Build();

        connection.On<RatesSnapshotModel>(RatesUpdatedMethod, snapshot => SnapshotReceived?.Invoke(snapshot));
        connection.Reconnecting += _ => { StateChanged?.Invoke(StreamState.Reconnecting); return Task.CompletedTask; };
        connection.Reconnected += _ => { StateChanged?.Invoke(StreamState.Connected); return Task.CompletedTask; };
    }

    public async Task StartAsync(CancellationToken token)
    {
        if (connection.State != HubConnectionState.Disconnected)
        {
            return;
        }

        while (true)
        {
            StateChanged?.Invoke(StreamState.Connecting);
            try
            {
                await connection.StartAsync(token);
                StateChanged?.Invoke(StreamState.Connected);
                return;
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                // The gateway is not up yet. WithAutomaticReconnect does not cover the very first connect.
                await Task.Delay(FirstConnectRetryDelay, token);
            }
        }
    }

    public Task StopAsync() => connection.StopAsync();

    public ValueTask DisposeAsync() => connection.DisposeAsync();

    // The built-in policy gives up after four attempts; a desktop client should keep trying.
    private sealed class EndlessRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext context) =>
            TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, context.PreviousRetryCount)));
    }
}