using CryptoRates.Gateway.Helpers;
using NLog;
using Scalar.AspNetCore;

namespace CryptoRates.Gateway.DependencyInjection;

internal static class ServiceRegisterExtension
{
    public static void RegisterApplicationLifetimeDelegates(this IHostApplicationLifetime hostApplicationLifetime,
        Logger logger)
    {
        hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            logger.Info(
                $"CryptocurrencyRates.Gateway has been started : [{ProgramRuntime.ProgramName}] ({ProgramRuntime.ProgramVersion}) Rev({ProgramRuntime.ProgramRevision})");
            logger.Info($"Program Version - {ProgramRuntime.ProgramVersion}");
        });

        hostApplicationLifetime.ApplicationStopped.Register(() =>
        {
            logger.Info(
                $"CryptocurrencyRates.Gateway has been stopped: [{ProgramRuntime.ProgramName}] ({ProgramRuntime.ProgramVersion}) Rev({ProgramRuntime.ProgramRevision})");
            logger.Info($"Program Version - {ProgramRuntime.ProgramVersion}");
        });
    }

    public static void RegisterScalar(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi(); // OpenAPI document at /openapi/v1.json

            app.MapScalarApiReference(options =>
            {
                options.WithTitle("Cryptocurrency Rates Gateway")
                       .WithTheme(ScalarTheme.Moon)
                       .ForceDarkMode();
            });
        }
    }
}