using Microsoft.AspNetCore.Diagnostics;

namespace LiberationFleet.Server.Api.Exceptions;

/// <summary>
/// Logs unhandled exceptions and returns a JSON body. Non-production includes the
/// exception message so local/docker toasts show the real failure instead of a blank 500.
/// </summary>
public sealed class UnhandledExceptionHandler(
    ILogger<UnhandledExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var expose = !environment.IsProduction() && !environment.IsStaging();
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = expose
                ? exception.Message
                : "An unexpected error occurred."
        }, cancellationToken);

        return true;
    }
}
