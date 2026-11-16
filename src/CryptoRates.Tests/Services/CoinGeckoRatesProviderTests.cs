using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Providers;
using CryptoRates.Tests.Services.TestHelpers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Net;
using Xunit;

namespace CryptoRates.Tests.Services;

public class CoinGeckoRatesProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private sealed record Sut(
        CoinGeckoRatesProvider Provider,
        StubHttpMessageHandler Handler,
        TestLogger<CoinGeckoRatesProvider> Logger,
        FakeTimeProvider Time);

    /// <summary>PageDelay is zero by default so tests do not wait; the delay has its own test.</summary>
    private static Sut Create(
        Func<RecordedRequest, HttpResponseMessage> responder,
        Action<RatesOptions>? configure = null,
        FakeTimeProvider? time = null)
    {
        var options = new RatesOptions { PageDelay = TimeSpan.Zero };
        configure?.Invoke(options);

        time ??= new FakeTimeProvider(Now);
        var handler = new StubHttpMessageHandler(responder);
        var client = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        var logger = new TestLogger<CoinGeckoRatesProvider>();

        return new Sut(new CoinGeckoRatesProvider(client, Options.Create(options), time, logger), handler, logger, time);
    }

    [Fact]
    public async Task GetRatesAsync_SingleShortPage_SendsOneRequestWithExpectedQuery()
    {
        var sut = Create(_ => Responses.Json(MarketsJson.Page(3)));

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(3, rates.Count);
        var request = Assert.Single(sut.Handler.Requests);
        Assert.Equal("/api/v3/coins/markets", request.Uri.AbsolutePath);
        Assert.Equal("usd", request.Query("vs_currency"));
        Assert.Equal("market_cap_desc", request.Query("order"));
        Assert.Equal("250", request.Query("per_page"));
        Assert.Equal("1", request.Query("page"));
        Assert.Equal("false", request.Query("sparkline"));
    }

    [Fact]
    public async Task GetRatesAsync_TwoFullPages_RequestsBothPages()
    {
        var sut = Create(
            r => Responses.Json(r.Query("page") == "1" ? MarketsJson.Page(250) : MarketsJson.Page(250, 251)),
            o => o.MaxCoins = 500);

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(500, rates.Count);
        Assert.Equal(new[] { "1", "2" }, sut.Handler.Requests.Select(r => r.Query("page")));
    }

    [Fact]
    public async Task GetRatesAsync_ShortPage_StopsPaging()
    {
        var sut = Create(
            r => Responses.Json(r.Query("page") == "1" ? MarketsJson.Page(250) : MarketsJson.Page(10, 251)),
            o => o.MaxCoins = 1000); // up to four pages allowed

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(260, rates.Count);
        Assert.Equal(2, sut.Handler.Requests.Count);
    }

    [Fact]
    public async Task GetRatesAsync_MultiplePages_WaitsPageDelayBetweenRequests()
    {
        var time = new FakeTimeProvider(Now);
        var seen = new List<DateTimeOffset>();
        var sut = Create(
            r =>
            {
                lock (seen)
                {
                    seen.Add(time.GetUtcNow());
                }

                return Responses.Json(r.Query("page") == "1" ? MarketsJson.Page(250) : MarketsJson.Page(10, 251));
            },
            o =>
            {
                o.MaxCoins = 500;
                o.PageDelay = TimeSpan.FromSeconds(2.5);
            },
            time);

        var task = sut.Provider.GetRatesAsync(CancellationToken.None);

        // Drive the fake clock until the provider finishes.
        for (var i = 0; i < 100 && !task.IsCompleted; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(500));
            await Task.Delay(5);
        }

        var rates = await task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(260, rates.Count);
        Assert.Equal(2, seen.Count);
        Assert.True(seen[1] - seen[0] >= TimeSpan.FromSeconds(2.5));
    }

    [Fact]
    public async Task GetRatesAsync_CoinRepeatedOnNextPage_IsKeptOnce()
    {
        // The ranking can shift between two requests, so a coin may show up on both pages.
        var firstPage = Enumerable.Range(1, 248).Select(i => new Coin($"x{i}", i, i))
            .Append(new Coin("a", 1m, 249))
            .Append(new Coin("b", 1m, 250));
        var secondPage = new[] { new Coin("B", 5m, 251), new Coin("c", 5m, 252) };

        var sut = Create(
            r => Responses.Json(r.Query("page") == "1" ? MarketsJson.OfMany(firstPage) : MarketsJson.OfMany(secondPage)),
            o => o.MaxCoins = 500);

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(251, rates.Count);
        var b = Assert.Single(rates, r => r.Id.Equals("b", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("b", b.Id); // the first occurrence wins
        Assert.Contains(rates, r => r.Id == "c");
    }

    [Fact]
    public async Task GetRatesAsync_MoreCoinsThanMaxCoins_KeepsTheFirstOnes()
    {
        var sut = Create(_ => Responses.Json(MarketsJson.Page(5)), o => o.MaxCoins = 2);

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(new[] { "coin1", "coin2" }, rates.Select(r => r.Id));
    }

    [Fact]
    public async Task GetRatesAsync_SkipsEntriesWithoutIdOrPrice()
    {
        var json = MarketsJson.Of(
            new Coin("ok1", 1m, 1),
            new Coin(null, 1m, 2),
            new Coin("", 1m, 3),
            new Coin("   ", 1m, 4),
            new Coin("no-price", null, 5),
            new Coin("ok2", 2m, 6));
        var sut = Create(_ => Responses.Json(json));

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(new[] { "ok1", "ok2" }, rates.Select(r => r.Id));
    }

    [Fact]
    public async Task GetRatesAsync_MapsAllFields()
    {
        var stamp = new DateTimeOffset(2026, 11, 15, 11, 30, 0, TimeSpan.Zero);
        var json = MarketsJson.Of(new Coin("bitcoin", 100000.5m, 1, "btc", "Bitcoin", stamp));
        var sut = Create(_ => Responses.Json(json));

        var rate = Assert.Single(await sut.Provider.GetRatesAsync(CancellationToken.None));

        Assert.Equal("bitcoin", rate.Id);
        Assert.Equal("BTC", rate.Symbol);
        Assert.Equal("Bitcoin", rate.Name);
        Assert.Equal(1, rate.Rank);
        Assert.Equal(100000.5m, rate.PriceUsd);
        Assert.Equal(stamp, rate.UpdatedAt);
    }

    [Fact]
    public async Task GetRatesAsync_MissingOptionalFields_FallBackToDefaults()
    {
        var sut = Create(_ => Responses.Json(MarketsJson.Of(new Coin("mystery", 1m))));

        var rate = Assert.Single(await sut.Provider.GetRatesAsync(CancellationToken.None));

        Assert.Equal("MYSTERY", rate.Symbol);      // symbol falls back to the id, upper-cased
        Assert.Equal("mystery", rate.Name);        // name falls back to the id
        Assert.Null(rate.Rank);
        Assert.Equal(Now, rate.UpdatedAt);         // last_updated falls back to the clock
    }

    [Fact]
    public async Task GetRatesAsync_SortsByRank_UnrankedCoinsLastThenById()
    {
        var json = MarketsJson.Of(
            new Coin("c3", 1m, 3),
            new Coin("zzz", 1m),
            new Coin("c1", 1m, 1),
            new Coin("aaa", 1m),
            new Coin("c2", 1m, 2));
        var sut = Create(_ => Responses.Json(json));

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(new[] { "c1", "c2", "c3", "aaa", "zzz" }, rates.Select(r => r.Id));
    }

    [Fact]
    public async Task GetRatesAsync_PinnedCoinsAlreadyInTop_NoExtraRequest()
    {
        var sut = Create(_ => Responses.Json(MarketsJson.Page(3)), o => o.PinnedCoins = ["coin2"]);

        await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Single(sut.Handler.Requests);
    }

    [Fact]
    public async Task GetRatesAsync_PinnedCoinsOutsideTop_AreFetchedAndMergedByRank()
    {
        var pinned = MarketsJson.Of(new Coin("terrausd", 0.01m, 400), new Coin("terra-luna", 0.0001m));
        var sut = Create(
            r => Responses.Json(r.HasQuery("ids") ? pinned : MarketsJson.Page(3)),
            o => o.PinnedCoins = ["terrausd", "terra-luna"]);

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(2, sut.Handler.Requests.Count);
        var second = sut.Handler.Requests[1];
        Assert.Equal("terrausd,terra-luna", second.Query("ids"));
        Assert.Equal("250", second.Query("per_page"));
        Assert.Equal(new[] { "coin1", "coin2", "coin3", "terrausd", "terra-luna" }, rates.Select(r => r.Id));
    }

    [Fact]
    public async Task GetRatesAsync_PinnedIdsAreDeduplicatedAndBlanksIgnored()
    {
        var sut = Create(
            r => Responses.Json(r.HasQuery("ids") ? MarketsJson.Of(new Coin("terrausd", 0.01m, 400)) : MarketsJson.Page(3)),
            o => o.PinnedCoins = ["terrausd", "TERRAUSD", " "]);

        await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal("terrausd", sut.Handler.Requests[1].Query("ids"));
    }

    [Fact]
    public async Task GetRatesAsync_PinnedRequestFails_ReturnsTopAndLogsWarning()
    {
        var sut = Create(
            r => r.HasQuery("ids") ? Responses.Status(HttpStatusCode.InternalServerError) : Responses.Json(MarketsJson.Page(3)),
            o => o.PinnedCoins = ["terrausd"]);

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(3, rates.Count);
        Assert.Contains(sut.Logger.Warnings, e => e.Exception is HttpRequestException);
    }

    [Fact]
    public async Task GetRatesAsync_PinnedCoinUnknownToProvider_LogsWarning()
    {
        var sut = Create(
            r => Responses.Json(r.HasQuery("ids") ? "[]" : MarketsJson.Page(3)),
            o => o.PinnedCoins = ["terrausd"]);

        var rates = await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.Equal(3, rates.Count);
        Assert.Contains(sut.Logger.Warnings, e => e.Message.Contains("terrausd"));
    }

    [Fact]
    public async Task GetRatesAsync_CancellationWhileLoadingPinned_IsNotSwallowed()
    {
        var sut = Create(
            r => r.HasQuery("ids") ? throw new OperationCanceledException() : Responses.Json(MarketsJson.Page(3)),
            o => o.PinnedCoins = ["terrausd"]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.Provider.GetRatesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetRatesAsync_WithoutApiKey_SendsNoKeyHeader()
    {
        var sut = Create(_ => Responses.Json(MarketsJson.Page(1)));

        await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.All(sut.Handler.Requests, r => Assert.Null(r.ApiKey));
    }

    [Fact]
    public async Task GetRatesAsync_WithApiKey_SendsItInTheDemoHeader()
    {
        var sut = Create(_ => Responses.Json(MarketsJson.Page(1)), o => o.ApiKey = "demo-key");

        await sut.Provider.GetRatesAsync(CancellationToken.None);

        Assert.All(sut.Handler.Requests, r => Assert.Equal("demo-key", r.ApiKey));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetRatesAsync_ErrorStatus_ThrowsHttpRequestException(HttpStatusCode status)
    {
        var sut = Create(_ => Responses.Status(status));

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.Provider.GetRatesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetRatesAsync_NullBody_ThrowsInvalidOperation()
    {
        var sut = Create(_ => Responses.Json("null"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Provider.GetRatesAsync(CancellationToken.None));

        Assert.Contains("empty response", ex.Message);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""[{"id":"x"},{"id":"y","current_price":null}]""")]
    public async Task GetRatesAsync_NoUsableRates_ThrowsInvalidOperation(string body)
    {
        var sut = Create(_ => Responses.Json(body));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.Provider.GetRatesAsync(CancellationToken.None));

        Assert.Contains("no usable rates", ex.Message);
    }
}
