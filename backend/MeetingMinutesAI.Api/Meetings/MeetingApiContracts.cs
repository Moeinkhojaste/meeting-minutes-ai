using System.ComponentModel.DataAnnotations;
using MeetingMinutesAI.Application.Meetings;
using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Api.Meetings;

public sealed record CreateMeetingRequest(
    [StringLength(300)] string? Title
);

public sealed record UpdateMeetingRequest(
    [StringLength(300)] string? Title
);

public sealed record ProcessingErrorResponse(
    string Code,
    string Message
);

public sealed record MeetingResponse(
    Guid Id,
    string? Title,
    string Status,
    ProcessingErrorResponse? ProcessingError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Version
)
{
    public static MeetingResponse FromApplication(MeetingView meeting) =>
        new(
            meeting.Id,
            meeting.Title,
            MapStatus(meeting.Status),
            MapError(meeting),
            meeting.CreatedAt,
            meeting.UpdatedAt,
            Convert.ToBase64String(meeting.Version));

    private static ProcessingErrorResponse? MapError(MeetingView meeting) =>
        meeting.ProcessingErrorCode is not null
            && meeting.ProcessingErrorMessage is not null
            ? new ProcessingErrorResponse(
                meeting.ProcessingErrorCode,
                meeting.ProcessingErrorMessage)
            : null;

    private static string MapStatus(MeetingProcessingStatus status) => status switch
    {
        MeetingProcessingStatus.Created => "created",
        MeetingProcessingStatus.Uploaded => "uploaded",
        MeetingProcessingStatus.Queued => "queued",
        MeetingProcessingStatus.Transcribing => "transcribing",
        MeetingProcessingStatus.GeneratingMinutes => "generatingMinutes",
        MeetingProcessingStatus.PartiallyCompleted => "partiallyCompleted",
        MeetingProcessingStatus.Completed => "completed",
        MeetingProcessingStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}

public sealed record MeetingPageResponse(
    IReadOnlyList<MeetingResponse> Items,
    int Page,
    int PageSize,
    int TotalCount
)
{
    public static MeetingPageResponse FromApplication(MeetingPage page) =>
        new(
            page.Items.Select(MeetingResponse.FromApplication).ToList(),
            page.Page,
            page.PageSize,
            page.TotalCount);
}
