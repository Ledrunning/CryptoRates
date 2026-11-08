using CryptocurrencyRates.Services.Configuration;
using Microsoft.Extensions.Options;

namespace CryptocurrencyRates.Gateway.Configuration;

public sealed class RatesOptionsValidator : IValidateOptions<RatesOptions>
{
    public static readonly TimeSpan MinimumPollInterval = TimeSpan.FromSeconds(30);

    public ValidateOptionsResult Validate(string? name, RatesOptions options)
    {
        var errors = new List<string>();

        if (options.BaseUrl is null || !options.BaseUrl.IsAbsoluteUri)
        {
            errors.Add("Rates:BaseUrl must be an absolute URL.");
        }

        if (options.PollInterval < MinimumPollInterval)
        {
            errors.Add($"Rates:PollInterval must be at least {MinimumPollInterval}.");
        }

        if (options.Coins.Count == 0)
        {
            errors.Add("Rates:Coins must contain at least one coin.");
        }

        if (options.Coins.Any(c => string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Symbol)))
        {
            errors.Add("Every entry in Rates:Coins needs both Id and Symbol.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
