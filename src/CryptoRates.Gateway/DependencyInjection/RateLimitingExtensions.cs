using CryptoRates.Services.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace CryptoRates.Gateway.DependencyInjection
{
    public static class RateLimitingExtensions
    {
        public const string RestPolicy = "rest";

        public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
                           ?? new RateLimitOptions();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                
                // A simple global limiter based on the client’s IP address
                options.AddFixedWindowLimiter("DefaultPolicy", opt =>
                {
                    opt.PermitLimit = settings.PermitLimit;
                    opt.Window = TimeSpan.FromSeconds(settings.WindowSeconds);
                    opt.QueueLimit = 0;
                });
            });

            return services;
        }
    }
}
