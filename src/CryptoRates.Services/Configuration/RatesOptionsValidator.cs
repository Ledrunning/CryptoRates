using CryptocurrencyRates.Services.Configuration;
using Microsoft.Extensions.Options;

namespace CryptoRates.Services.Configuration;

public sealed class RatesOptionsValidator : IValidateOptions<RatesOptions>
{
    public static readonly TimeSpan MinimumPollInterval = TimeSpan.FromSeconds(30);

    public ValidateOptionsResult Validate(string? name, RatesOptions o)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _) || !o.BaseUrl.EndsWith('/'))
        {
            failures.Add("Rates:BaseUrl must be an absolute URL ending with '/'.");
        }

        if (o.PollInterval < TimeSpan.FromSeconds(30))
        {
            failures.Add("Rates:PollInterval must be at least 30 seconds.");
        }

        if (o.MaxCoins is < 1 or > 1000)
        {
            failures.Add("Rates:MaxCoins must be between 1 and 1000.");
        }

        if (o.PinnedCoins.Count > RatesOptions.MaxPinned)
        {
            failures.Add($"Rates:PinnedCoins can hold at most {RatesOptions.MaxPinned} ids.");
        }

        if (o.PinnedCoins.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Rates:PinnedCoins must not contain empty ids.");
        }

        var callsPerMonth = o.MaxCallsPerPoll * (TimeSpan.FromDays(30) / o.PollInterval);
        if (callsPerMonth > o.MonthlyCallBudget)
        {
            failures.Add($"Rates: about {callsPerMonth:N0} calls/month exceeds the budget of " +
                         $"{o.MonthlyCallBudget:N0}. Increase PollInterval or reduce MaxCoins/PinnedCoins.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
