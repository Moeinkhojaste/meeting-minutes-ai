using Microsoft.AspNetCore.Diagnostics;
using MeetingMinutesAI.Application.Meetings;
using MeetingMinutesAI.Domain.Common;

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

        var error = exception switch
        {
            MeetingNotFoundException => new ErrorDetails(
                StatusCodes.Status404NotFound,
                "meeting_not_found",
                "The meeting was not found."),
            MeetingConcurrencyException => new ErrorDetails(
                StatusCodes.Status409Conflict,
                "concurrency_conflict",
                "The meeting changed. Refresh it and try again."),
            MeetingActiveProcessingException => new ErrorDetails(
                StatusCodes.Status409Conflict,
                "meeting_active",
                "An actively processing meeting cannot be deleted."),
            DomainRuleException => new ErrorDetails(
                StatusCodes.Status400BadRequest,
                "invalid_request",
                "The request is invalid."),
            _ => new ErrorDetails(
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "The request could not be completed."),
        };

        if (error.StatusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled request failure. CorrelationId: {CorrelationId}",
                correlationId
            );
        }

        httpContext.Response.StatusCode = error.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ApiError(
                error.Code,
                error.Message,
                correlationId
            ),
            cancellationToken
        );
        return true;
    }

    private sealed record ErrorDetails(
        int StatusCode,
        string Code,
        string Message
    );
}
