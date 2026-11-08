using CryptoRates.Gateway.Helpers;

namespace CryptoRates.Gateway.Endpoints
{
    public static class VersionEndpoints
    {
        public static IEndpointRouteBuilder MapVersionEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/version", () => $"Version - {ProgramRuntime.ProgramVersion}")
                .WithTags("Service")
                .WithSummary("Gateway version");

            return app;
        }
    }
}