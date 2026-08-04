using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Abstractions.Ai;

public interface IAiServiceClient
{
    Task<AiTranscriptionResult> TranscribeAsync(
        Stream audio,
        string fileName,
        string contentType,
        ProcessingMode mode,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<AiMinutesResult> GenerateMinutesAsync(
        AiRawTranscript rawTranscript,
        ProcessingMode mode,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public enum AiFailureKind
{
    InvalidRequest,
    Unavailable,
    Timeout,
    InvalidResponse,
}

public sealed class AiServiceException : Exception
{
    public AiServiceException(
        AiFailureKind kind,
        string code,
        string safeMessage,
        bool retryable,
        Exception? innerException = null)
        : base(safeMessage, innerException)
    {
        Kind = kind;
        Code = code;
        SafeMessage = safeMessage;
        Retryable = retryable;
    }

    public AiFailureKind Kind { get; }
    public string Code { get; }
    public string SafeMessage { get; }
    public bool Retryable { get; }
}

public sealed record AiRawTranscript(
    int SchemaVersion,
    IReadOnlyList<AiRawSegment> Segments
);

public sealed record AiRawSegment(
    string Id,
    string? Speaker,
    long StartMilliseconds,
    long EndMilliseconds,
    string Language,
    string Text
);

public sealed record AiCleanedTranscript(
    int SchemaVersion,
    IReadOnlyList<AiCleanedSegment> Segments
);

public sealed record AiCleanedSegment(
    string Id,
    string Text,
    IReadOnlyList<string> SourceRawSegmentIds
);

public sealed record AiStageMetadata(
    string Stage,
    ProcessingMode RequestedMode,
    string PrimaryProvider,
    string PrimaryModel,
    string ActualProvider,
    string ActualModel,
    bool FallbackUsed,
    string? FallbackReason,
    string PromptVersion,
    int SchemaVersion,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    long DurationMilliseconds
);

public sealed record AiTranscriptionResult(
    AiRawTranscript RawTranscript,
    AiStageMetadata Metadata,
    string CorrelationId
);

public sealed record AiMinutesResult(
    AiCleanedTranscript CleanedTranscript,
    AiGeneratedMinutes Minutes,
    AiStageMetadata Metadata,
    string CorrelationId
);

public sealed record AiGeneratedMinutes(
    int SchemaVersion,
    string? Title,
    string? Date,
    string Summary,
    IReadOnlyList<AiParticipant> Participants,
    IReadOnlyList<AiTopic> Topics,
    IReadOnlyList<AiDecision> Decisions,
    IReadOnlyList<AiActionItem> ActionItems,
    IReadOnlyList<AiOpenQuestion> OpenQuestions,
    IReadOnlyList<AiUncertainty> Uncertainties
);

public sealed record AiParticipant(
    string Name,
    IReadOnlyList<string> EvidenceSegmentIds
);

public sealed record AiTopic(
    string Title,
    string Summary,
    IReadOnlyList<string> EvidenceSegmentIds
);

public sealed record AiDecision(
    string Text,
    IReadOnlyList<string> EvidenceSegmentIds
);

public sealed record AiActionItem(
    string Task,
    string? Assignee,
    string? Deadline,
    IReadOnlyList<string> EvidenceSegmentIds
);

public sealed record AiOpenQuestion(
    string Text,
    IReadOnlyList<string> EvidenceSegmentIds
);

public sealed record AiUncertainty(
    string Field,
    string Description,
    IReadOnlyList<string> EvidenceSegmentIds
);
