using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Models;
using CryptocurrencyRates.Services.Providers;
using CryptoRates.Services.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using System.Text;

namespace CryptoRates.Services.Tests;

public class CoinGeckoRatesProviderTests
{
    private static CoinGeckoRatesProvider CreateProvider(
        StubHttpMessageHandler handler,
        RatesOptions? options = null,
        FakeTimeProvider? time = null,
        ILogger<CoinGeckoRatesProvider>? logger = null)
    {
        options ??= new RatesOptions { BaseUrl = "https://api.coingecko.com/api/v3/", MaxCoins = 250 };
        time ??= new FakeTimeProvider();
        logger ??= new FakeLogger<CoinGeckoRatesProvider>();
        var client = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        return new CoinGeckoRatesProvider(client, Options.Create(options), time, logger);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task GetRatesAsync_SinglePageUnder250Coins_OneRequestWithExpectedQuery()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    MarketsResponseBuilder.Array(
                        MarketsResponseBuilder.Coin(
                            "bitcoin", symbol: "btc", name: "Bitcoin", currentPrice: 50000m, marketCapRank: 1)))
            });
        var provider = CreateProvider(handler, time: time);

        // Act
        var rates = await provider.GetRatesAsync(CancellationToken.None);

        // Assert
        Assert.Single(handler.Requests);
        var url = handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("vs_currency=usd", url);
        Assert.Contains("order=market_cap_desc", url);
        Assert.Contains("per_page=250", url);
        Assert.Contains("page=1", url);

        Assert.Single(rates);
        var rate = rates[0];
        Assert.Equal("bitcoin", rate.Id);
        Assert.Equal("BTC", rate.Symbol);
        Assert.Equal("Bitcoin", rate.Name);
        Assert.Equal(1, rate.Rank);
        Assert.Equal(50000m, rate.PriceUsd);
        Assert.Equal(time.GetUtcNow(), rate.UpdatedAt);
    }

    [Fact]
    public async Task GetRatesAsync_MaxCoins500_TwoRequests_StopsWhenPageShort()
    {
        // Arrange
        var options = new RatesOptions { MaxCoins = 500 };
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(MarketsResponseBuilder.Series(250)) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(MarketsResponseBuilder.Series(100, firstId: 251)) });
        var provider = CreateProvider(handler, options);

        // Act
        var rates = await provider.GetRatesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("page=1", handler.Requests[0].RequestUri!.Query);
        Assert.Contains("page=2", handler.Requests[1].RequestUri!.Query);
        Assert.Equal(350, rates.Count);
    }

    [Fact]
    public async Task GetRatesAsync_MaxCoins500_FirstPageShort_OneRequestOnly()
    {
        // Arrange
        var options = new RatesOptions { MaxCoins = 500 };
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(MarketsResponseBuilder.Series(200)) });
        var provider = CreateProvider(handler, options);

        // Act
        var rates = await provider.GetRatesAsync(CancellationToken.None);

        // Assert
        Assert.Single(handler.Requests);
        Assert.Equal(200, rates.Count);
    }

    [Fact]
    public async Task GetRatesAsync_PageDelayBetweenPages_SecondRequestOnlyAfterTimeAdvance()
    {
        // Arrange
        var options = new RatesOptions { MaxCoins = 500, PageDelay = TimeSpan.FromSeconds(2.5) };
        var time = new FakeTimeProvider();
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(MarketsResponseBuilder.Series(250)) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(MarketsResponseBuilder.Series(250, firstId: 251)) });
        var provider = CreateProvider(handler, options, time);

        // Act
        var task = provider.GetRatesAsync(CancellationToken.None);

        Assert.False(task.IsCompleted);
        Assert.Equal(1, handler.Requests.Count);

        time.Advance(options.PageDelay);

        var rates = await task;
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("page=2", handler.Requests[1].RequestUri!.Query);
        Assert.Equal(500, rates.Count);
    }

    [Fact]
    public async Task GetRatesAsync_DuplicateIdsAcrossPages_CollapsedCaseInsensitive_TrimmedToMaxCoins()
    {
        // Arrange
        var options = new RatesOptions { MaxCoins = 500 };
        var page1Coins = new List<string>
        {
            MarketsResponseBuilder.Coin("bitcoin", symbol: "btc", marketCapRank: 1),
            MarketsResponseBuilder.Coin("ethereum", symbol: "eth", marketCapRank: 2)
        };
        page1Coins.AddRange(
            Enumerable.Range(3, 248).Select(i => MarketsResponseBuilder.Coin($"coin-{i}", marketCapRank: i)));
        var page1 = MarketsResponseBuilder.Array(page1Coins.ToArray());
        var page2 = MarketsResponseBuilder.Array(
            MarketsResponseBuilder.Coin("BITCOIN", marketCapRank: 99),
            MarketsResponseBuilder.Coin("ethereum", marketCapRank: 98),
            MarketsResponseBuilder.Coin("ripple", symbol: "xrp", marketCapRank: 251));
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(page1) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = Json(page2) });
        var provider = CreateProvider(handler, options);

        // Act
        var rates = await provider.GetRatesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(251, rates.Count);
        Assert.Equal(1, rates.Count(r => r.Id.Equals("bitcoin", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(1, rates.Count(r => r.Id.Equals("ethereum", StringComparison.OrdinalIgnoreCase)));
        Assert.Single(rates, r => r.Id == "ripple");
        Assert.True(
            rates.SequenceEqual(
                rates.OrderBy(r => r.Rank ?? int.MaxValue).ThenBy(r => r.Id, StringComparer.Ordinal)));
    }

    [Fact]
    public async Task GetRatesAsync_SkipItemsWithoutPriceAndEmptyId()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    MarketsResponseBuilder.Array(
                        MarketsResponseBuilder.Coin("", currentPrice: 10m),
                        MarketsResponseBuilder.Coin("skip-me", symbol: "SKIP"),
                        MarketsResponseBuilder.Coin("keep", symbol: "k", currentPrice: 2.5m)))
            });
        var provider = CreateProvider(handler);

        // Act
        var rates = await provider.GetRatesAsync(CancellationToken.None);

        // Assert
        Assert.Single(rates);
        Assert.Equal("keep", rates[0].Id);
        Assert.Equal("K", rates[0].Symbol);
        Assert.Equal(2.5m, rates[0].PriceUsd);
    }

    [Fact]
    public async Task GetRatesAsync_SymbolUpper_NameDefaultsToId_LastUpdatedFromTimeProvider()
    {
        // Arrange
        var time = new FakeTimeProvider();
        time.SetUtcNow(new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero));
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    MarketsResponseBuilder.Array(
                        MarketsResponseBuilder.Coin("cardano", symbol: "ada", currentPrice: 1.23m)))
            });
        var provider = CreateProvider(handler, time: time);

        // Act
        var rates = await provider.GetRatesAsync(CancellationToken.None);

        // Assert
        Assert.Single(rates);
        Assert.Equal("cardano", rates[0].Name);
        Assert.Equal("ADA", rates[0].Symbol);
        Assert.Equal(time.GetUtcNow(), rates[0].UpdatedAt);
    }

    [Fact]
    public async Task GetRatesAsync_PinnedAllInTop_NoExtraRequest()
    {
        // Arrange
        var options = new RatesOptions { MaxCoins = 250, PinnedCoins = ["bitcoin"] };
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    MarketsResponseBuilder.Array(
                        MarketsResponseBuilder.Coin("bitcoin", marketCapRank: 1),
                        MarketsResponseBuilder.Coin("solana", marketCapRank: 2)))
            });
        var provider = CreateProvider(handler, options);

        // Act
        var rates = await provider.GetRatesAsync(CancellationToken.None);

        // Assert
        Assert.Single(handler.Requests);
        Assert.Equal(2, rates.Count);
        Assert.Equal("bitcoin", rates[0].Id);
    }

    [Fact]
    public async Task GetRatesAsync_PinnedPartiallyMissing_ExtraRequestWithEscapedIds_CombinedSorted_UnknownLogged()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new FakeLogger<CoinGeckoRatesProvider>();
        var options = new RatesOptions
        {
            MaxCoins = 250,
            PageDelay = TimeSpan.FromSeconds(2.5),
            PinnedCoins = ["bitcoin", "ethereum", "no%such"]
        };
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    MarketsResponseBuilder.Array(
                        MarketsResponseBuilder.Coin("bitcoin", marketCapRank: 1),
                        MarketsResponseBuilder.Coin("solana", marketCapRank: 2)))
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(MarketsResponseBuilder.Coin("ethereum", marketCapRank: 5))
            });
        var provider = CreateProvider(handler, options, time, logger);

        // Act
        var task = provider.GetRatesAsync(CancellationToken.None);
        Assert.False(task.IsCompleted);
        Assert.Equal(1, handler.Requests.Count);

        time.Advance(options.PageDelay);

        var rates = await task;

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        var pinnedUrl = handler.Requests[1].RequestUri!.ToString();
        Assert.Contains("ids=", pinnedUrl);
        Assert.Contains("ethereum", pinnedUrl);
        Assert.Contains("no%25such", pinnedUrl);

        Assert.Equal(3, rates.Count);
        Assert.Equal([1, 2, 5], rates.Select(r => r.Rank).ToList());
        Assert.DoesNotContain(rates, r => r.Id == "no%such");

        var warnings = logger.LogRecords().Where(r => r.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains("no%such", warnings[0].Message);
    }

    [Fact]
    public async Task GetRatesAsync_PinnedFetchFails500_ReturnsTopOnly_LogsWarning()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new FakeLogger<CoinGeckoRatesProvider>();
        var options = new RatesOptions
        {
            MaxCoins = 250,
            PageDelay = TimeSpan.FromSeconds(2.5),
            PinnedCoins = ["ethereum"]
        };
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(MarketsResponseBuilder.Array(MarketsResponseBuilder.Coin("bitcoin", marketCapRank: 1)))
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var provider = CreateProvider(handler, options, time, logger);

        // Act
        var task = provider.GetRatesAsync(CancellationToken.None);
        Assert.False(task.IsCompleted);
        Assert.Equal(1, handler.Requests.Count);

        time.Advance(options.PageDelay);

        var rates = await task;

        // Assert
        Assert.Single(rates);
        Assert.Equal("bitcoin", rates[0].Id);
        var warnings = logger.LogRecords().Where(r => r.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains("Pinned coins could not be loaded", warnings[0].Message);
    }

    [Fact]
    public async Task GetRatesAsync_CancellationDuringPinned_NotSwallowed()
    {
        // Arrange
        var options = new RatesOptions
        {
            MaxCoins = 250,
            PageDelay = TimeSpan.FromSeconds(2.5),
            PinnedCoins = ["ethereum"]
        };
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(MarketsResponseBuilder.Array(MarketsResponseBuilder.Coin("bitcoin", marketCapRank: 1)))
            },
            new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(handler, options);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act / Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.GetRatesAsync(cts.Token));
    }

    [Fact]
    public async Task GetRatesAsync_ApiKeyHeaderSentOnlyWhenConfigured()
    {
        // Arrange: без ключа — заголовка не отправляется
        var noKeyHandler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("[]") });
        var noKeyProvider = CreateProvider(
            noKeyHandler,
            new RatesOptions { BaseUrl = "https://api.coingecko.com/api/v3/", ApiKey = null });

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => noKeyProvider.GetRatesAsync(CancellationToken.None));

        // Assert
        Assert.False(noKeyHandler.Requests[0].Headers.Contains(CoinGeckoRatesProvider.ApiKeyHeader));

        // Arrange: с ключом — заголовок есть
        var keyHandler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = Json("[]") });
        var keyProvider = CreateProvider(
            keyHandler,
            new RatesOptions { BaseUrl = "https://api.coingecko.com/api/v3/", ApiKey = "demo-key-123" });

        // Act
        await Assert.ThrowsAnyAsync<Exception>(() => keyProvider.GetRatesAsync(CancellationToken.None));

        // Assert
        Assert.True(keyHandler.Requests[0].Headers.Contains(CoinGeckoRatesProvider.ApiKeyHeader));
        Assert.Equal(
            "demo-key-123",
            keyHandler.Requests[0].Headers.GetValues(CoinGeckoRatesProvider.ApiKeyHeader).First());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetRatesAsync_429Or500_ThrowsHttpRequestException(HttpStatusCode status)
    {
        // Arrange
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(status));
        var provider = CreateProvider(handler);

        // Act / Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetRatesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetRatesAsync_EmptyBody_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var provider = CreateProvider(handler);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRatesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetRatesAsync_NoUsableRates_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json(
                    MarketsResponseBuilder.Array(
                        MarketsResponseBuilder.Coin("", currentPrice: 10m),
                        MarketsResponseBuilder.Coin("skip-me")))
            });
        var provider = CreateProvider(handler);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetRatesAsync(CancellationToken.None));
    }
}