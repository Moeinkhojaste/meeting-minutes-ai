namespace MeetingMinutesAI.Domain.Meetings;

public enum MeetingProcessingStatus
{
    Created,
    Uploaded,
    Queued,
    Transcribing,
    GeneratingMinutes,
    PartiallyCompleted,
    Completed,
    Failed,
}

public enum MeetingMinutesKind
{
    Generated,
    Final,
}

public enum ProcessingMode
{
    Fast,
    Quality,
}

public enum ProcessingRunStatus
{
    Running,
    PartiallyCompleted,
    Completed,
    Failed,
}

public enum ProcessingStageKind
{
    Transcription,
    Minutes,
}
