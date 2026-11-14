using Microsoft.AspNetCore.Diagnostics;

namespace CryptoRates.Gateway.ErrorHandling
{
    internal sealed class GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetails) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
        {
            // The client went away: nothing to send, and not an error worth logging.
            if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                return true;
            }

            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = exception,
                ProblemDetails = { Title = "An unexpected error occurred.", Status = StatusCodes.Status500InternalServerError }
            });
        }
    }
}
