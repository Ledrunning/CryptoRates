using CryptoRates.Gateway.ErrorHandling;
using System.Runtime.CompilerServices;

namespace CryptoRates.Gateway.DependencyInjection
{
    public static class ErrorHandlingExtensions
    {
        public static IServiceCollection AddErrorHandling(this IServiceCollection services)
        {
            services.AddProblemDetails(o => o.CustomizeProblemDetails = c 
                    => c.ProblemDetails.Extensions["traceId"] = c.HttpContext.TraceIdentifier);
            services.AddExceptionHandler<GlobalExceptionHandler>();

            return services;
        }
    }
}
