using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Meetings;

internal static class MeetingViewMapper
{
    public static MeetingView Map(Meeting meeting) =>
        new(
            meeting.Id,
            meeting.Title,
            meeting.UserId,
            meeting.Status,
            meeting.ProcessingErrorCode,
            meeting.ProcessingErrorMessage,
            meeting.AudioFile is null
                ? null
                : new AudioView(
                    meeting.AudioFile.OriginalFileName,
                    meeting.AudioFile.ContentType,
                    meeting.AudioFile.ByteLength,
                    meeting.AudioFile.DurationMilliseconds,
                    meeting.AudioFile.UploadedAt),
            meeting.CreatedAt,
            meeting.UpdatedAt,
            meeting.RowVersion.ToArray());
}
