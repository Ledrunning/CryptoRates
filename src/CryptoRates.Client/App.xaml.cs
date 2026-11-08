using CryptocurrencyRates.Common.Exceptions;
using CryptoCurrencyRates.Client.Configuration;
using CryptoCurrencyRates.Client.Contracts;
using CryptoRates.Client.Contracts;
using CryptoRates.Client.Services.Hub;
using CryptoRates.Client.Services.Rest;
using CryptoRates.Client.View;
using CryptoRates.Client.ViewModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CryptoRates.Client;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider? ServiceProvider { get; private set; }
    public IConfiguration? Configuration { get; private set; }

    protected override void OnStartup(StartupEventArgs eventArgs)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true);

        Configuration = builder.Build();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ServiceProvider = serviceCollection.BuildServiceProvider();
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var gatewayUrl = Configuration?.GetSection(GatewaySettings.SectionName).Get<GatewaySettings>()?.GatewayUrl;

        if (!Uri.TryCreate(gatewayUrl, UriKind.Absolute, out var gatewayUri))
        {
            throw new CryptoCurrencyRatesException("Error to reading gateway settings: GatewayUrl is missing or invalid.");
        }

        var settings = Configuration?.GetSection(GatewaySettings.SectionName).Get<GatewaySettings>();

        if (!Uri.TryCreate(settings?.HubUrl, UriKind.Absolute, out var hubUri))
        {
            throw new CryptoCurrencyRatesException("Error to reading gateway settings: HubUrl is missing or invalid.");
        }

        services.AddSingleton<IRatesStream>(_ => new RatesHubClient(hubUri));

        services.AddHttpClient<ICryptoCurrencyRatesService, CryptoCurrencyRatesService>(client =>
        {
            client.BaseAddress = gatewayUri;
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddSingleton<MainViewModel>();
        services.AddSingleton(sp => new MainWindow { DataContext = sp.GetRequiredService<MainViewModel>() });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ServiceProvider is IAsyncDisposable disposable)
        {
            Task.Run(() => disposable.DisposeAsync().AsTask()).GetAwaiter().GetResult();
        }

        base.OnExit(e);
    }
}