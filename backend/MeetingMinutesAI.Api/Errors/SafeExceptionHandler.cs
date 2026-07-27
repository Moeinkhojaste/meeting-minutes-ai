using Microsoft.AspNetCore.Diagnostics;

namespace MeetingMinutesAI.Api.Errors;

public sealed class SafeExceptionHandler(
    ILogger<SafeExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var correlationId =
            httpContext.TraceIdentifier ?? Guid.NewGuid().ToString("N");

        logger.LogError(
            exception,
            "Unhandled request failure. CorrelationId: {CorrelationId}",
            correlationId
        );

        httpContext.Response.StatusCode =
            StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new ApiError(
                "internal_error",
                "The request could not be completed.",
                correlationId
            ),
            cancellationToken
        );
        return true;
    }
}
