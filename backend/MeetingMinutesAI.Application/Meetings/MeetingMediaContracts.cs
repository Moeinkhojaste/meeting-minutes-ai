using MeetingMinutesAI.Application.Abstractions.Ai;

namespace MeetingMinutesAI.Application.Meetings;

public sealed record RawTranscriptView(
    int SchemaVersion,
    IReadOnlyList<RawSegmentView> Segments,
    DateTimeOffset CreatedAt,
    byte[] MeetingVersion
);

public sealed record RawSegmentView(
    string Id,
    string? Speaker,
    long StartMilliseconds,
    long EndMilliseconds,
    string Language,
    string Text
);

public sealed record CleanedTranscriptView(
    int SchemaVersion,
    IReadOnlyList<CleanedSegmentView> Segments,
    DateTimeOffset CreatedAt,
    byte[] MeetingVersion
);

public sealed record CleanedSegmentView(
    string Id,
    string Text,
    IReadOnlyList<string> SourceRawSegmentIds
);

public sealed record GeneratedMinutesView(
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
    byte[] MeetingVersion
);

public sealed record ParticipantView(string Name, IReadOnlyList<string> EvidenceSegmentIds);
public sealed record TopicView(
    string Title,
    string Summary,
    IReadOnlyList<string> EvidenceSegmentIds);
public sealed record DecisionView(string Text, IReadOnlyList<string> EvidenceSegmentIds);
public sealed record ActionItemView(
    string Task,
    string? Assignee,
    string? Deadline,
    IReadOnlyList<string> EvidenceSegmentIds);
public sealed record OpenQuestionView(string Text, IReadOnlyList<string> EvidenceSegmentIds);
public sealed record UncertaintyView(
    string Field,
    string Description,
    IReadOnlyList<string> EvidenceSegmentIds);

public sealed class MeetingOutputNotFoundException : Exception
{
    public MeetingOutputNotFoundException(string outputName)
        : base($"The meeting {outputName} is not available.")
    {
        OutputName = outputName;
    }

    public string OutputName { get; }
}

public sealed class MeetingCommandConflictException : Exception
{
    public MeetingCommandConflictException(string safeMessage)
        : base(safeMessage)
    {
    }
}

public sealed class MeetingProcessingException : Exception
{
    public MeetingProcessingException(AiServiceException failure)
        : base(failure.SafeMessage, failure)
    {
        Failure = failure;
    }

    public AiServiceException Failure { get; }
}
