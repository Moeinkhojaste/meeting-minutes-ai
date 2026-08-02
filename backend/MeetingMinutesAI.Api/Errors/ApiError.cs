namespace MeetingMinutesAI.Api.Errors;

public sealed record ApiError(
    string Code,
    string Message,
    string CorrelationId
);
