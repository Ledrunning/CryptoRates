using CryptocurrencyRates.Services.Configuration;
using CryptoRates.Services.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CryptoRates.Tests.Services;

public class RatesOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(Action<RatesOptions>? configure = null)
    {
        var options = new RatesOptions();
        configure?.Invoke(options);
        return new RatesOptionsValidator().Validate(null, options);
    }

    private static List<string> Pinned(int count) =>
        Enumerable.Range(1, count).Select(i => $"pinned{i}").ToList();

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        Assert.True(Validate().Succeeded);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("https://api.coingecko.com/api/v3")] // no trailing slash: HttpClient would cut off /v3/
    [InlineData("/api/v3/")]
    public void Validate_BadBaseUrl_Fails(string baseUrl)
    {
        var result = Validate(o => o.BaseUrl = baseUrl);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("BaseUrl"));
    }

    [Fact]
    public void Validate_PollIntervalBelowMinimum_Fails()
    {
        var result = Validate(o => o.PollInterval = TimeSpan.FromSeconds(29));

        Assert.Contains(result.Failures!, f => f.Contains("PollInterval"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Validate_MaxCoinsOutOfRange_Fails(int maxCoins)
    {
        var result = Validate(o => o.MaxCoins = maxCoins);

        Assert.Contains(result.Failures!, f => f.Contains("MaxCoins must be between"));
    }

    [Fact]
    public void Validate_TooManyPinnedCoins_Fails()
    {
        var result = Validate(o => o.PinnedCoins = Pinned(RatesOptions.MaxPinned + 1));

        Assert.Contains(result.Failures!, f => f.Contains("at most 50"));
    }

    [Fact]
    public void Validate_EmptyPinnedId_Fails()
    {
        var result = Validate(o => o.PinnedCoins = ["terrausd", "  "]);

        Assert.Contains(result.Failures!, f => f.Contains("empty ids"));
    }

    [Theory]
    // maxCoins, pinned, interval in minutes, expected to fit the 10 000 calls/month budget
    [InlineData(250, 0, 10, true)]   // 1 call  x 4 320
    [InlineData(250, 2, 10, true)]   // 2 calls x 4 320 = 8 640
    [InlineData(250, 2, 5, false)]   // 2 calls x 8 640 = 17 280
    [InlineData(250, 0, 5, true)]    // 1 call  x 8 640
    [InlineData(250, 0, 1, false)]   // 1 call  x 43 200
    [InlineData(500, 0, 10, true)]   // 2 calls x 4 320 = 8 640
    [InlineData(500, 2, 10, false)]  // 3 calls x 4 320 = 12 960
    [InlineData(1000, 0, 40, true)]  // 4 calls x 1 080 = 4 320
    public void Validate_MonthlyQuota_IsChecked(int maxCoins, int pinned, int intervalMinutes, bool expectedValid)
    {
        var result = Validate(o =>
        {
            o.MaxCoins = maxCoins;
            o.PinnedCoins = Pinned(pinned);
            o.PollInterval = TimeSpan.FromMinutes(intervalMinutes);
        });

        Assert.Equal(expectedValid, result.Succeeded);
        if (!expectedValid)
        {
            Assert.Contains(result.Failures!, f => f.Contains("calls/month"));
        }
    }

    [Fact]
    public void Validate_BudgetBoundary_IsInclusive()
    {
        // Defaults: 1 call every 10 minutes = exactly 4 320 calls in 30 days.
        Assert.True(Validate(o => o.MonthlyCallBudget = 4320).Succeeded);
        Assert.True(Validate(o => o.MonthlyCallBudget = 4319).Failed);
    }

    [Fact]
    public void Validate_SeveralProblems_AreAllReported()
    {
        var result = Validate(o =>
        {
            o.BaseUrl = "nope";
            o.PollInterval = TimeSpan.FromSeconds(1);
        });

        Assert.True(result.Failures!.Count() >= 2);
    }
}
