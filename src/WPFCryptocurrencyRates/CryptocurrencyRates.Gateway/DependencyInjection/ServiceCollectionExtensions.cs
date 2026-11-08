using CryptocurrencyRates.Gateway.Configuration;
using CryptocurrencyRates.Gateway.Health;
using CryptocurrencyRates.Gateway.Hubs;
using CryptocurrencyRates.Gateway.Services;
using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Contracts;
using CryptocurrencyRates.Services.Providers;
using CryptocurrencyRates.Services.Services;
using Microsoft.Extensions.Options;

namespace CryptocurrencyRates.Gateway.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRates(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RatesOptions>()
            .Bind(configuration.GetSection(RatesOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RatesOptions>, RatesOptionsValidator>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RatesCache>();
        services.AddSingleton<IRatesNotifier, SignalRRatesNotifier>();

        // Typed HttpClient with the standard resilience pipeline (retry, timeout, circuit breaker).
        services.AddHttpClient<IRatesProvider, CoinGeckoRatesProvider>((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<RatesOptions>>().Value;
                client.BaseAddress = WithTrailingSlash(settings.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CryptocurrencyRates.Gateway/1.0");
            })
            .AddStandardResilienceHandler();

        services.AddHostedService<RatesPollingService>();
        services.AddHealthChecks().AddCheck<RatesHealthCheck>("rates");

        return services;
    }

    private static Uri WithTrailingSlash(Uri uri)
    {
        var text = uri.AbsoluteUri;
        return text.EndsWith('/') ? uri : new Uri(text + "/");
    }
}
