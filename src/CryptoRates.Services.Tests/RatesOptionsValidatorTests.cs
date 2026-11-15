using CryptocurrencyRates.Services.Configuration;
using Microsoft.Extensions.Options;

namespace CryptoRates.Services.Tests;

public class RatesOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(Action<RatesOptions> configure)
    {
        var options = new RatesOptions();
        configure(options);
        return new RatesOptionsValidator().Validate(null, options);
    }

    [Fact]
    public void Validate_Defaults_Success()
    {
        var result = Validate(_ => { });
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("api.coingecko.com/api/v3/")]      // относительный URL
    [InlineData("https://api.coingecko.com/api/v3")] // без завершающего слэша
    public void Validate_InvalidBaseUrl_Fail(string baseUrl)
    {
        var result = Validate(o => o.BaseUrl = baseUrl);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Rates:BaseUrl"));
    }

    [Fact]
    public void Validate_ValidBaseUrl_Success()
    {
        var result = Validate(o => o.BaseUrl = "https://api.coingecko.com/api/v3/");
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(0)]
    public void Validate_PollIntervalLessThan30Seconds_Fail(int seconds)
    {
        var result = Validate(o => o.PollInterval = TimeSpan.FromSeconds(seconds));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Rates:PollInterval"));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(3600)]
    public void Validate_PollIntervalAtLeast30Seconds_Success(int seconds)
    {
        var result = Validate(o => o.PollInterval = TimeSpan.FromSeconds(seconds));
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Validate_MaxCoinsOutOfRange_Fail(int maxCoins)
    {
        var result = Validate(o => o.MaxCoins = maxCoins);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Rates:MaxCoins"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    [InlineData(1000)]
    public void Validate_MaxCoinsInRange_Success(int maxCoins)
    {
        var result = Validate(o => o.MaxCoins = maxCoins);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_PinnedCoinsMoreThan50_Fail()
    {
        var result = Validate(o => o.PinnedCoins = Enumerable.Range(1, 51).Select(i => $"coin-{i}").ToList());

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Rates:PinnedCoins"));
    }

    [Fact]
    public void Validate_PinnedCoinsFifty_Success()
    {
        var result = Validate(o => o.PinnedCoins = Enumerable.Range(1, 50).Select(i => $"coin-{i}").ToList());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EmptyPinnedId_Fail()
    {
        var result = Validate(o => o.PinnedCoins = ["bitcoin", " ", ""]);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("Rates:PinnedCoins"));
    }

    [Theory]
    [InlineData("10:00", true)]   // 250 монет + закреплённые, 10 минут: 2 × 4 320 = 8 640 < 10 000
    [InlineData("05:00", false)]  // 5 минут: 2 × 8 640 = 17 280 > 10 000
    public void Validate_QuotaWithPinned(string pollInterval, bool expected)
    {
        var options = new RatesOptions
        {
            MaxCoins = 250,
            PinnedCoins = ["bitcoin", "ethereum"],
            PollInterval = TimeSpan.Parse(pollInterval)
        };
        var result = new RatesOptionsValidator().Validate(null, options);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public void Validate_FiveHundredCoinsTenMinutes_Success()
    {
        var result = Validate(o =>
        {
            o.MaxCoins = 500;
            o.PollInterval = TimeSpan.FromMinutes(10);
        });
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MonthlyCallBudgetBoundary_Success()
    {
        // Ровно 10 000 вызовов в месяц: 1 вызов/опрос × (30 дней / 43,2 мин) = 10 000.
        var result = Validate(o => o.PollInterval = TimeSpan.FromSeconds(2592));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MonthlyCallBudgetExceeded_Fail()
    {
        var result = Validate(o => o.PollInterval = TimeSpan.FromSeconds(2591));
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Contains("budget"));
    }

    [Fact]
    public void Validate_AllErrorsNameTheSettings()
    {
        var result = Validate(o =>
        {
            o.BaseUrl = "relative";
            o.PollInterval = TimeSpan.FromSeconds(10);
            o.MaxCoins = 0;
            o.PinnedCoins = new List<string> { "", "bitcoin" };
            o.PinnedCoins.AddRange(Enumerable.Range(1, 49).Select(i => $"coin-{i}"));
        });

        Assert.False(result.Succeeded);
        Assert.All(result.Errors, e => Assert.Contains("Rates:", e));
    }
}