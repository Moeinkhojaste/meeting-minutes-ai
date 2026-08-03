using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Meetings;

public sealed record MeetingView(
    Guid Id,
    string? Title,
    MeetingProcessingStatus Status,
    string? ProcessingErrorCode,
    string? ProcessingErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Version
);

public sealed record MeetingPage(
    IReadOnlyList<MeetingView> Items,
    int Page,
    int PageSize,
    int TotalCount
);

public sealed class MeetingNotFoundException : Exception
{
    public MeetingNotFoundException()
        : base("The meeting was not found.")
    {
    }
}

public sealed class MeetingConcurrencyException : Exception
{
    public MeetingConcurrencyException()
        : base("The meeting changed before the operation completed.")
    {
    }
}

public sealed class MeetingActiveProcessingException : Exception
{
    public MeetingActiveProcessingException()
        : base("An actively processing meeting cannot be deleted.")
    {
    }
}
