using CryptoCurrencyRates.Client.ViewModel.Commands;
using CryptoRates.Client.Contracts;
using CryptoRates.Client.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace CryptoRates.Client.ViewModel;

public sealed class MainViewModel : BaseViewModel
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher; // the VM is created on the UI thread
    private readonly IRatesStream stream;

    private string buttonText = ApplicationResource.Start;
    private CancellationTokenSource? cts;

    private bool isRunning;

    public bool IsRunning
    {
        get => isRunning;
        private set => SetField(ref isRunning, value);
    }

    private string searchText = string.Empty;

    public ICollectionView CryptoCurrencyView { get; }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (searchText == value) return;
            searchText = value;
            OnPropertyChanged();
            CryptoCurrencyView.Refresh();
        }
    }

    private bool Matches(object item) =>
        item is CurrencyRateModel c &&
        (string.IsNullOrWhiteSpace(searchText) ||
         c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
         c.Symbol.Contains(searchText, StringComparison.OrdinalIgnoreCase));

    private DateTimeOffset? lastUpdate;
    private string status = "Stopped";

    public MainViewModel(IRatesStream stream)
    {
        this.stream = stream;
        CryptoCurrencyView = CollectionViewSource.GetDefaultView(CryptoCurrencyCollection);
        CryptoCurrencyView.Filter = Matches;
        stream.SnapshotReceived += OnSnapshotReceived;
        stream.StateChanged += OnStateChanged;

        // ToggleAsync catches everything, so nothing is lost in the discarded task.
        StartServiceCommand = new RelayCommand(() => _ = ToggleAsync());
    }

    public ObservableCollection<CurrencyRateModel> CryptoCurrencyCollection { get; } = new();

    public ICommand StartServiceCommand { get; }

    public string ButtonText
    {
        get => buttonText;
        private set => SetField(ref buttonText, value);
    }

    public string Status
    {
        get => status;
        private set => SetField(ref status, value);
    }

    public DateTimeOffset? LastUpdate
    {
        get => lastUpdate;
        private set => SetField(ref lastUpdate, value);
    }

    private async Task ToggleAsync()
    {
        try
        {
            if (IsRunning)
            {
                await StopAsync();
            }
            else
            {
                await StartAsync();
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}"; // shown in the status line, not in a MessageBox loop
        }
    }

    private async Task StartAsync()
    {
        IsRunning = true;
        ButtonText = ApplicationResource.Stop;
        cts = new CancellationTokenSource();

        try
        {
            await stream.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Stop was pressed while the client was still connecting.
        }
    }

    private async Task StopAsync()
    {
        IsRunning = false;
        ButtonText = ApplicationResource.Start;

        cts?.Cancel();
        cts?.Dispose();
        cts = null;

        await stream.StopAsync();
        Status = "Stopped";
    }

    // SignalR events arrive on a background thread, the UI collection must be touched on the UI thread.
    private void OnSnapshotReceived(RatesSnapshotModel snapshot) =>
        dispatcher.InvokeAsync(() =>
        {
            Apply(snapshot.Rates);
            LastUpdate = snapshot.FetchedAt.ToLocalTime();
        });

    private void OnStateChanged(StreamState state) =>
        dispatcher.InvokeAsync(() => Status = state switch
        {
            StreamState.Connecting => "Connecting to the gateway...",
            StreamState.Reconnecting => "Connection lost, reconnecting...",
            _ => "Connected"
        });

    /// <summary>Updates rows in place so the DataGrid does not flicker or lose the selection.</summary>
    private void Apply(IReadOnlyList<CurrencyRateModel> incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var existing = CryptoCurrencyCollection.ToDictionary(r => r.Id);
        var incomingIds = incoming.Select(r => r.Id).ToHashSet();

        foreach (var gone in CryptoCurrencyCollection.Where(r => !incomingIds.Contains(r.Id)).ToList())
        {
            CryptoCurrencyCollection.Remove(gone);
        }

        for (var i = 0; i < incoming.Count; i++)
        {
            var item = incoming[i];
            if (existing.TryGetValue(item.Id, out var row))
            {
                row.UpdateFrom(item);
                var current = CryptoCurrencyCollection.IndexOf(row);
                if (current != i)
                {
                    CryptoCurrencyCollection.Move(current, i); // keeps the market-cap order
                }
            }
            else
            {
                CryptoCurrencyCollection.Insert(i, item);
            }
        }
    }
}