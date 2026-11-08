using CryptocurrencyRates.Gateway.Models;
using CryptocurrencyRates.Services.Models;
using CryptocurrencyRates.Services.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CryptocurrencyRates.Gateway.Endpoints
{
    public static class RatesEndpoints
    {
        public static IEndpointRouteBuilder MapRatesEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/v1/rates").WithTags("Rates");

            group.MapGet("/", GetAll)
                .WithSummary("Latest rates for all tracked coins");

            group.MapGet("/{id}", GetById)
                .WithSummary("Latest rate for one coin (id is case-insensitive, e.g. bitcoin)");

            return app;
        }

        private static Results<Ok<RatesSnapshot>, ProblemHttpResult> GetAll(RatesCache cache)
        {
            if (cache.Snapshot is not { } snapshot)
            {
                return NotReady();
            }

            return TypedResults.Ok(snapshot);
        }

        private static Results<Ok<CryptoRate>, NotFound, ProblemHttpResult> GetById(string id, RatesCache cache)
        {
            if (cache.Snapshot is not { } snapshot)
            {
                return NotReady();
            }

            var rate = snapshot.Rates.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
            return rate is null ? TypedResults.NotFound() : TypedResults.Ok(rate);
        }

        private static ProblemHttpResult NotReady()
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Rates are not available yet.");
        }
    }
}
