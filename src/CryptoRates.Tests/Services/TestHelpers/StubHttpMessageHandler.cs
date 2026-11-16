using CryptocurrencyRates.Services.Providers;
using System.Net;
using System.Text;
using System.Web;

namespace CryptoRates.Tests.Services.TestHelpers;

/// <summary>What the provider sent, captured at send time (the request itself is disposed afterwards).</summary>
internal sealed record RecordedRequest(Uri Uri, string? ApiKey)
{
    public string Query(string name) => HttpUtility.ParseQueryString(Uri.Query)[name] ?? string.Empty;

    public bool HasQuery(string name) => HttpUtility.ParseQueryString(Uri.Query)[name] is not null;
}

internal sealed class StubHttpMessageHandler(Func<RecordedRequest, HttpResponseMessage> responder) : HttpMessageHandler
{
    private readonly List<RecordedRequest> requests = [];

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (requests)
            {
                return [.. requests];
            }
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? apiKey = request.Headers.TryGetValues(CoinGeckoRatesProvider.ApiKeyHeader, out var values)
            ? values.Single()
            : null;

        var recorded = new RecordedRequest(request.RequestUri!, apiKey);
        lock (requests)
        {
            requests.Add(recorded);
        }

        return Task.FromResult(responder(recorded));
    }
}

internal static class Responses
{
    public static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode code) => new(code);
}
