using CryptocurrencyRates.Gateway.Services;
using CryptocurrencyRates.Services.Configuration;
using CryptocurrencyRates.Services.Contracts;
using CryptocurrencyRates.Services.Providers;
using CryptocurrencyRates.Services.Services;
using CryptoRates.Gateway.Health;
using CryptoRates.Gateway.Hubs;
using CryptoRates.Services.Configuration;
using Microsoft.Extensions.Options;

namespace CryptoRates.Gateway.DependencyInjection;

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
                client.BaseAddress = new Uri(settings.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CryptocurrencyRates.Gateway/1.0");
            })
            .AddStandardResilienceHandler();

        services.AddHostedService<RatesPollingService>();
        services.AddHealthChecks().AddCheck<RatesHealthCheck>("rates");

        return services;
    }
}