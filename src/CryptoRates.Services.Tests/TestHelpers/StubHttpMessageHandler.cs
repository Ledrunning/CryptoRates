using System.Net;
using System.Text;
using System.Text.Json;

namespace CryptoRates.Services.Tests.TestHelpers;

/// <summary>
///     Заглушка <see cref="HttpMessageHandler" />: запоминает все запросы и отдаёт
///     ответы из очереди (или по функции). Сеть не используется.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public readonly List<HttpRequestMessage> Requests = new();

    private readonly Queue<HttpResponseMessage> _responses = new();
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? _func;

    public StubHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        foreach (var response in responses)
        {
            _responses.Enqueue(response);
        }
    }

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
    {
        _func = func;
    }

    public HttpResponseMessage Queue(HttpResponseMessage response)
    {
        _responses.Enqueue(response);
        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_func is { } func)
        {
            return func(request, cancellationToken);
        }

        return _responses.TryDequeue(out var response)
            ? Task.FromResult(response)
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    public static HttpResponseMessage Json(object content)
    {
        var json = content is string s ? s : System.Text.Json.JsonSerializer.Serialize(content);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
