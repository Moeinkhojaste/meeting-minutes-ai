using MeetingMinutesAI.Domain.Common;

namespace MeetingMinutesAI.Domain.Meetings;

public sealed class ProcessingRun
{
    private readonly List<ProcessingStage> _stages = [];

    private ProcessingRun()
    {
    }

    internal ProcessingRun(
        Guid meetingId,
        int attemptNumber,
        ProcessingMode requestedMode,
        string correlationId,
        DateTimeOffset startedAt)
    {
        if (attemptNumber <= 0)
        {
            throw new DomainRuleException("Processing attempt number must be positive.");
        }

        Id = Guid.NewGuid();
        MeetingId = meetingId;
        AttemptNumber = attemptNumber;
        RequestedMode = requestedMode;
        CorrelationId = Guard.Required(correlationId, "Correlation ID", 100);
        Status = ProcessingRunStatus.Running;
        StartedAt = Guard.Utc(startedAt);
    }

    public Guid Id { get; private set; }

    public Guid MeetingId { get; private set; }

    public int AttemptNumber { get; private set; }

    public ProcessingMode RequestedMode { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public ProcessingRunStatus Status { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool? ErrorRetryable { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public long? TotalDurationMilliseconds { get; private set; }

    public IReadOnlyCollection<ProcessingStage> Stages => _stages.AsReadOnly();

    public void AddStage(
        ProcessingStageKind stage,
        string primaryProvider,
        string primaryModel,
        string actualProvider,
        string actualModel,
        bool fallbackUsed,
        string? fallbackReason,
        string promptVersion,
        int schemaVersion,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        long durationMilliseconds)
    {
        EnsureRunning();
        if (_stages.Any(item => item.Stage == stage))
        {
            throw new DomainRuleException("Processing stage is already recorded.");
        }

        _stages.Add(new ProcessingStage(
            Id,
            stage,
            primaryProvider,
            primaryModel,
            actualProvider,
            actualModel,
            fallbackUsed,
            fallbackReason,
            promptVersion,
            schemaVersion,
            startedAt,
            completedAt,
            durationMilliseconds));
    }

    public void Complete(DateTimeOffset completedAt, long totalDurationMilliseconds) =>
        Finish(
            ProcessingRunStatus.Completed,
            completedAt,
            totalDurationMilliseconds,
            null,
            null,
            null);

    public void CompletePartially(
        string errorCode,
        string errorMessage,
        bool retryable,
        DateTimeOffset completedAt,
        long totalDurationMilliseconds) =>
        Finish(
            ProcessingRunStatus.PartiallyCompleted,
            completedAt,
            totalDurationMilliseconds,
            errorCode,
            errorMessage,
            retryable);

    public void Fail(
        string errorCode,
        string errorMessage,
        bool retryable,
        DateTimeOffset completedAt,
        long totalDurationMilliseconds) =>
        Finish(
            ProcessingRunStatus.Failed,
            completedAt,
            totalDurationMilliseconds,
            errorCode,
            errorMessage,
            retryable);

    private void Finish(
        ProcessingRunStatus status,
        DateTimeOffset completedAt,
        long totalDurationMilliseconds,
        string? errorCode,
        string? errorMessage,
        bool? retryable)
    {
        EnsureRunning();
        if (totalDurationMilliseconds < 0)
        {
            throw new DomainRuleException("Processing duration cannot be negative.");
        }

        Status = status;
        ErrorCode = Guard.Optional(errorCode, "Processing error code", 100);
        ErrorMessage = Guard.Optional(errorMessage, "Processing error message", 1000);
        ErrorRetryable = retryable;
        CompletedAt = Guard.Utc(completedAt);
        TotalDurationMilliseconds = totalDurationMilliseconds;
    }

    private void EnsureRunning()
    {
        if (Status != ProcessingRunStatus.Running)
        {
            throw new DomainRuleException("Processing run is already finished.");
        }
    }
}

public sealed class ProcessingStage
{
    private ProcessingStage()
    {
    }

    internal ProcessingStage(
        Guid processingRunId,
        ProcessingStageKind stage,
        string primaryProvider,
        string primaryModel,
        string actualProvider,
        string actualModel,
        bool fallbackUsed,
        string? fallbackReason,
        string promptVersion,
        int schemaVersion,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        long durationMilliseconds)
    {
        if (schemaVersion <= 0 || durationMilliseconds < 0 || completedAt < startedAt)
        {
            throw new DomainRuleException("Processing stage metadata is invalid.");
        }

        Id = Guid.NewGuid();
        ProcessingRunId = processingRunId;
        Stage = stage;
        PrimaryProvider = Guard.Required(primaryProvider, "Primary provider", 100);
        PrimaryModel = Guard.Required(primaryModel, "Primary model", 200);
        ActualProvider = Guard.Required(actualProvider, "Actual provider", 100);
        ActualModel = Guard.Required(actualModel, "Actual model", 200);
        FallbackUsed = fallbackUsed;
        FallbackReason = Guard.Optional(fallbackReason, "Fallback reason", 1000);
        PromptVersion = Guard.Required(promptVersion, "Prompt version", 200);
        SchemaVersion = schemaVersion;
        StartedAt = Guard.Utc(startedAt);
        CompletedAt = Guard.Utc(completedAt);
        DurationMilliseconds = durationMilliseconds;
    }

    public Guid Id { get; private set; }
    public Guid ProcessingRunId { get; private set; }
    public ProcessingStageKind Stage { get; private set; }
    public string PrimaryProvider { get; private set; } = string.Empty;
    public string PrimaryModel { get; private set; } = string.Empty;
    public string ActualProvider { get; private set; } = string.Empty;
    public string ActualModel { get; private set; } = string.Empty;
    public bool FallbackUsed { get; private set; }
    public string? FallbackReason { get; private set; }
    public string PromptVersion { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }
    public long DurationMilliseconds { get; private set; }
}
