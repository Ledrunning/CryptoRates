# CryptoRates

**Work in progress.** The gateway and the WPF client already work; tests, Docker, CI and the web client are still on the way.

A small real-time crypto price dashboard: one gateway talks to the market data provider, clients only talk to the gateway.

CoinGecko ──► Gateway (ASP.NET Core) ──► WPF client
│ REST + SignalR └──► Vue web client (planned)


## What it does

- Polls CoinGecko `/coins/markets` and keeps the top 250 coins by market cap, plus any extra coins you pin in the config
- Caches the latest snapshot, so clients never hit the external API
- Serves it over REST (`GET /api/v1/rates`, `GET /api/v1/rates/{id}`) and pushes updates over SignalR (`/hubs/rates`, event `RatesUpdated`)
- WPF client with live updates, search and a Material Design dark theme

## Tech stack

- **Gateway:** .NET 10, ASP.NET Core minimal API, SignalR, `BackgroundService` + `PeriodicTimer`, `HttpClient` with the standard resilience handler, OpenAPI + Scalar (Development only)
- **Desktop client:** WPF, MVVM, SignalR client, `System.Text.Json`, MaterialDesignInXamlToolkit
- **Web client (planned):** Vue 3, Vite, TypeScript
- **Planned:** xUnit tests, Docker, GitHub Actions

## Running locally

Requirements: .NET 10 SDK (the WPF client needs Windows).

```bash
dotnet dev-certs https --trust

# optional but recommended: a free CoinGecko Demo API key
dotnet user-secrets set "Rates:ApiKey" "<your key>" --project src/CryptoRates.Gateway

dotnet run --project src/CryptoRates.Gateway   # https://localhost:7208, API docs at /scalar
dotnet run --project src/CryptoRates.Client    # WPF client, press Start
```

The poll interval defaults to 10 minutes to stay inside the free Demo quota (10,000 calls per month). The gateway validates this on startup and refuses a configuration that would exceed it.

## Roadmap

- [ ] Unit and integration tests
- [ ] Dockerfile and docker-compose for the gateway
- [ ] GitHub Actions CI
- [ ] Vue 3 web client
- [ ] Screenshots / GIFs

## Credits

Market data by [CoinGecko](https://www.coingecko.com).

## License

TBD