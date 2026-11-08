using CryptoRates.Client.ViewModel;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CryptoRates.Client.Model;

/// <summary>
///     One rate as returned by the new gateway (GET /api/v1/rates).
///     <see cref="RateUsd" /> keeps its old name so the existing DataGrid binding keeps working.
/// </summary>
public sealed class CurrencyRateModel : BaseViewModel
{
    private string name = string.Empty;
    private int? rank;
    private decimal rateUsd;
    private DateTimeOffset updatedAt;

    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("symbol")] public string Symbol { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get => name; set => SetField(ref name, value); }
    [JsonPropertyName("rank")] public int? Rank { get => rank; set => SetField(ref rank, value); }
    [JsonPropertyName("priceUsd")] public decimal RateUsd { get => rateUsd; set => SetField(ref rateUsd, value); }
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get => updatedAt; set => SetField(ref updatedAt, value); }

    public void UpdateFrom(CurrencyRateModel other)
    {
        Name = other.Name;
        Rank = other.Rank;
        RateUsd = other.RateUsd;
        UpdatedAt = other.UpdatedAt;
    }
}

/// <summary>
///     Response of GET /api/v1/rates.
/// </summary>
public sealed class RatesSnapshotModel
{
    [JsonPropertyName("rates")] public List<CurrencyRateModel> Rates { get; set; } = [];
    [JsonPropertyName("fetchedAt")] public DateTimeOffset FetchedAt { get; set; }
}