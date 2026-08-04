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

public sealed record AudioResponse(
    string OriginalFileName,
    string ContentType,
    long ByteLength,
    long? DurationMilliseconds,
    DateTimeOffset UploadedAt
);

public sealed record MeetingResponse(
    Guid Id,
    string? Title,
    string Status,
    ProcessingErrorResponse? ProcessingError,
    AudioResponse? Audio,
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
            meeting.Audio is null
                ? null
                : new AudioResponse(
                    meeting.Audio.OriginalFileName,
                    meeting.Audio.ContentType,
                    meeting.Audio.ByteLength,
                    meeting.Audio.DurationMilliseconds,
                    meeting.Audio.UploadedAt),
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

public sealed record ProcessMeetingRequest(string? Mode);

public sealed record RawTranscriptResponse(
    int SchemaVersion,
    IReadOnlyList<RawSegmentView> Segments,
    DateTimeOffset CreatedAt,
    string Version)
{
    public static RawTranscriptResponse FromApplication(RawTranscriptView value) =>
        new(value.SchemaVersion, value.Segments, value.CreatedAt,
            Convert.ToBase64String(value.MeetingVersion));
}

public sealed record CleanedTranscriptResponse(
    int SchemaVersion,
    IReadOnlyList<CleanedSegmentView> Segments,
    DateTimeOffset CreatedAt,
    string Version)
{
    public static CleanedTranscriptResponse FromApplication(CleanedTranscriptView value) =>
        new(value.SchemaVersion, value.Segments, value.CreatedAt,
            Convert.ToBase64String(value.MeetingVersion));
}

public sealed record GeneratedMinutesResponse(
    int SchemaVersion,
    string? Title,
    string? Date,
    string Summary,
    IReadOnlyList<ParticipantView> Participants,
    IReadOnlyList<TopicView> Topics,
    IReadOnlyList<DecisionView> Decisions,
    IReadOnlyList<ActionItemView> ActionItems,
    IReadOnlyList<OpenQuestionView> OpenQuestions,
    IReadOnlyList<UncertaintyView> Uncertainties,
    DateTimeOffset CreatedAt,
    string Version)
{
    public static GeneratedMinutesResponse FromApplication(GeneratedMinutesView value) =>
        new(
            value.SchemaVersion,
            value.Title,
            value.Date,
            value.Summary,
            value.Participants,
            value.Topics,
            value.Decisions,
            value.ActionItems,
            value.OpenQuestions,
            value.Uncertainties,
            value.CreatedAt,
            Convert.ToBase64String(value.MeetingVersion));
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
