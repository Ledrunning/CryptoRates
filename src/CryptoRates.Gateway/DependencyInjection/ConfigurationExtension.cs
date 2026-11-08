using NLog;
using NLog.Web;
using System.Diagnostics;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace CryptoRates.Gateway.DependencyInjection;

internal static class ConfigurationExtension
{
    public static void ConfigureLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Host.UseNLog();

        Trace.Listeners.Clear();
        Trace.Listeners.Add(new NLogTraceListener());
    }
}