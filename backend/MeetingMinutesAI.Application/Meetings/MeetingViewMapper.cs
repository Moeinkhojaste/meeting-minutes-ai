using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Meetings;

internal static class MeetingViewMapper
{
    public static MeetingView Map(Meeting meeting)
    {
        var latestRun = meeting.ProcessingRuns
            .OrderByDescending(run => run.AttemptNumber)
            .FirstOrDefault();

        return new(
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
            latestRun is null
                ? null
                : new ProcessingRunView(
                    latestRun.AttemptNumber,
                    latestRun.RequestedMode.ToString().ToLowerInvariant(),
                    latestRun.Status.ToString().ToLowerInvariant(),
                    latestRun.Stages.Select(stage => new ProcessingStageView(
                        stage.Stage.ToString().ToLowerInvariant(),
                        stage.PrimaryProvider,
                        stage.PrimaryModel,
                        stage.ActualProvider,
                        stage.ActualModel,
                        stage.FallbackUsed,
                        stage.FallbackReason,
                        stage.DurationMilliseconds)).ToList()),
            meeting.CreatedAt,
            meeting.UpdatedAt,
            meeting.RowVersion.ToArray());
    }
}
