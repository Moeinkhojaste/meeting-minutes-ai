using MeetingMinutesAI.Domain.Common;

namespace MeetingMinutesAI.Domain.Meetings;

public sealed class Meeting
{
    private readonly List<MeetingMinutes> _minutes = [];
    private readonly List<ProcessingRun> _processingRuns = [];

    private Meeting()
    {
    }

    private Meeting(string? title, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Title = Guard.Optional(title, "Meeting title", 300);
        Status = MeetingProcessingStatus.Created;
        CreatedAt = Guard.Utc(createdAt);
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public string? Title { get; private set; }

    public MeetingProcessingStatus Status { get; private set; }

    public string? ProcessingErrorCode { get; private set; }

    public string? ProcessingErrorMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public AudioFile? AudioFile { get; private set; }

    public RawTranscript? RawTranscript { get; private set; }

    public CleanedTranscript? CleanedTranscript { get; private set; }

    public IReadOnlyCollection<MeetingMinutes> Minutes => _minutes.AsReadOnly();

    public IReadOnlyCollection<ProcessingRun> ProcessingRuns => _processingRuns.AsReadOnly();

    public static Meeting Create(string? title, DateTimeOffset createdAt) =>
        new(title, createdAt);

    public void Rename(string? title, DateTimeOffset updatedAt)
    {
        Title = Guard.Optional(title, "Meeting title", 300);
        Touch(updatedAt);
    }

    public void AttachAudio(
        string originalFileName,
        string storageKey,
        string contentType,
        long byteLength,
        long? durationMilliseconds,
        string sha256,
        DateTimeOffset uploadedAt)
    {
        EnsureStatus(MeetingProcessingStatus.Created);
        if (AudioFile is not null)
        {
            throw new DomainRuleException("Meeting already has audio.");
        }

        AudioFile = new AudioFile(
            Id,
            originalFileName,
            storageKey,
            contentType,
            byteLength,
            durationMilliseconds,
            sha256,
            uploadedAt);
        Status = MeetingProcessingStatus.Uploaded;
        ClearError();
        Touch(uploadedAt);
    }

    public void Queue(DateTimeOffset queuedAt)
    {
        if (Status is not (
            MeetingProcessingStatus.Uploaded
            or MeetingProcessingStatus.Completed
            or MeetingProcessingStatus.PartiallyCompleted
            or MeetingProcessingStatus.Failed))
        {
            throw new DomainRuleException("Meeting cannot be queued from its current status.");
        }

        if (Status is MeetingProcessingStatus.Completed
            or MeetingProcessingStatus.PartiallyCompleted
            or MeetingProcessingStatus.Failed)
        {
            RawTranscript = null;
            CleanedTranscript = null;
            _minutes.Clear();
        }

        Status = MeetingProcessingStatus.Queued;
        ClearError();
        Touch(queuedAt);
    }

    public ProcessingRun StartTranscription(
        ProcessingMode mode,
        string correlationId,
        DateTimeOffset startedAt)
    {
        EnsureStatus(MeetingProcessingStatus.Queued);
        var run = new ProcessingRun(
            Id,
            _processingRuns.Count + 1,
            mode,
            correlationId,
            startedAt);
        _processingRuns.Add(run);
        Status = MeetingProcessingStatus.Transcribing;
        Touch(startedAt);
        return run;
    }

    public void SetRawTranscript(
        ProcessingRun run,
        int schemaVersion,
        IEnumerable<RawTranscriptSegment> segments,
        DateTimeOffset createdAt)
    {
        EnsureActiveRun(run);
        EnsureStatus(MeetingProcessingStatus.Transcribing);
        if (RawTranscript is not null)
        {
            throw new DomainRuleException("Meeting already has a raw transcript.");
        }

        RawTranscript = RawTranscript.Create(Id, run.Id, schemaVersion, segments, createdAt);
        Touch(createdAt);
    }

    public void StartMinutesGeneration(ProcessingRun run, DateTimeOffset startedAt)
    {
        EnsureActiveRun(run);
        EnsureStatus(MeetingProcessingStatus.Transcribing);
        if (RawTranscript is null)
        {
            throw new DomainRuleException("Minutes generation requires a raw transcript.");
        }

        Status = MeetingProcessingStatus.GeneratingMinutes;
        Touch(startedAt);
    }

    public void SetCleanedTranscript(
        ProcessingRun run,
        int schemaVersion,
        IEnumerable<CleanedTranscriptSegment> segments,
        DateTimeOffset createdAt)
    {
        EnsureActiveRun(run);
        EnsureStatus(MeetingProcessingStatus.GeneratingMinutes);
        if (RawTranscript is null)
        {
            throw new DomainRuleException("A cleaned transcript requires a raw transcript.");
        }

        if (CleanedTranscript is not null)
        {
            throw new DomainRuleException("Meeting already has a cleaned transcript.");
        }

        var validRawIds = RawTranscript.Segments.Select(segment => segment.Id).ToHashSet();
        var materialized = segments.ToList();
        if (materialized.SelectMany(segment => segment.Sources)
            .Any(source => !validRawIds.Contains(source.RawTranscriptSegmentId)))
        {
            throw new DomainRuleException("Cleaned transcript references unknown raw segments.");
        }

        CleanedTranscript = CleanedTranscript.Create(
            Id,
            run.Id,
            schemaVersion,
            materialized,
            createdAt);
        Touch(createdAt);
    }

    public void AttachGeneratedMinutes(
        ProcessingRun run,
        MeetingMinutes generatedMinutes,
        DateTimeOffset attachedAt)
    {
        EnsureActiveRun(run);
        EnsureStatus(MeetingProcessingStatus.GeneratingMinutes);
        if (RawTranscript is null || CleanedTranscript is null)
        {
            throw new DomainRuleException("Generated minutes require both transcript forms.");
        }

        if (generatedMinutes.MeetingId != Id
            || generatedMinutes.ProcessingRunId != run.Id
            || generatedMinutes.Kind != MeetingMinutesKind.Generated)
        {
            throw new DomainRuleException("Generated minutes do not belong to this processing run.");
        }

        if (_minutes.Any(item => item.Kind == MeetingMinutesKind.Generated))
        {
            throw new DomainRuleException("Meeting already has generated minutes.");
        }

        EnsureEvidenceBelongsToRawTranscript(generatedMinutes);
        generatedMinutes.LockGenerated();
        _minutes.Add(generatedMinutes);
        Touch(attachedAt);
    }

    public MeetingMinutes CreateFinalMinutes(DateTimeOffset createdAt)
    {
        if (_minutes.Any(item => item.Kind == MeetingMinutesKind.Final))
        {
            throw new DomainRuleException("Meeting already has final minutes.");
        }

        var generated = _minutes.SingleOrDefault(
            item => item.Kind == MeetingMinutesKind.Generated)
            ?? throw new DomainRuleException("Final minutes require generated minutes.");
        var final = generated.CreateFinalCopy(createdAt);
        _minutes.Add(final);
        Touch(createdAt);
        return final;
    }

    public void Complete(ProcessingRun run, DateTimeOffset completedAt)
    {
        EnsureActiveRun(run);
        EnsureStatus(MeetingProcessingStatus.GeneratingMinutes);
        if (CleanedTranscript is null
            || _minutes.All(item => item.Kind != MeetingMinutesKind.Generated))
        {
            throw new DomainRuleException("Completed processing requires all generated output.");
        }

        if (run.Status != ProcessingRunStatus.Completed)
        {
            throw new DomainRuleException("Processing run must be completed first.");
        }

        Status = MeetingProcessingStatus.Completed;
        ClearError();
        Touch(completedAt);
    }

    public void CompletePartially(
        ProcessingRun run,
        string errorCode,
        string errorMessage,
        DateTimeOffset completedAt)
    {
        EnsureActiveRun(run);
        EnsureStatus(MeetingProcessingStatus.GeneratingMinutes);
        if (RawTranscript is null || run.Status != ProcessingRunStatus.PartiallyCompleted)
        {
            throw new DomainRuleException("Partial processing requires a raw transcript and partial run.");
        }

        Status = MeetingProcessingStatus.PartiallyCompleted;
        SetError(errorCode, errorMessage);
        Touch(completedAt);
    }

    public void Fail(
        ProcessingRun run,
        string errorCode,
        string errorMessage,
        DateTimeOffset failedAt)
    {
        EnsureActiveRun(run);
        if (Status is not (
            MeetingProcessingStatus.Transcribing
            or MeetingProcessingStatus.GeneratingMinutes)
            || run.Status != ProcessingRunStatus.Failed)
        {
            throw new DomainRuleException("Meeting cannot fail from its current status.");
        }

        Status = MeetingProcessingStatus.Failed;
        SetError(errorCode, errorMessage);
        Touch(failedAt);
    }

    private void EnsureEvidenceBelongsToRawTranscript(MeetingMinutes minutes)
    {
        var validRawIds = RawTranscript!.Segments.Select(segment => segment.Id).ToHashSet();
        var evidenceIds = minutes.Participants.SelectMany(item => item.Evidence)
            .Select(link => link.RawTranscriptSegmentId)
            .Concat(minutes.Topics.SelectMany(item => item.Evidence)
                .Select(link => link.RawTranscriptSegmentId))
            .Concat(minutes.Decisions.SelectMany(item => item.Evidence)
                .Select(link => link.RawTranscriptSegmentId))
            .Concat(minutes.ActionItems.SelectMany(item => item.Evidence)
                .Select(link => link.RawTranscriptSegmentId))
            .Concat(minutes.OpenQuestions.SelectMany(item => item.Evidence)
                .Select(link => link.RawTranscriptSegmentId))
            .Concat(minutes.Uncertainties.SelectMany(item => item.Evidence)
                .Select(link => link.RawTranscriptSegmentId));

        if (evidenceIds.Any(id => !validRawIds.Contains(id)))
        {
            throw new DomainRuleException("Meeting minutes reference unknown raw segments.");
        }
    }

    private void EnsureActiveRun(ProcessingRun run)
    {
        if (run.MeetingId != Id || _processingRuns.LastOrDefault()?.Id != run.Id)
        {
            throw new DomainRuleException("Processing run does not belong to this meeting.");
        }
    }

    private void EnsureStatus(MeetingProcessingStatus required)
    {
        if (Status != required)
        {
            throw new DomainRuleException("Meeting is not in the required processing status.");
        }
    }

    private void SetError(string code, string message)
    {
        ProcessingErrorCode = Guard.Required(code, "Processing error code", 100);
        ProcessingErrorMessage = Guard.Required(message, "Processing error message", 1000);
    }

    private void ClearError()
    {
        ProcessingErrorCode = null;
        ProcessingErrorMessage = null;
    }

    private void Touch(DateTimeOffset updatedAt) => UpdatedAt = Guard.Utc(updatedAt);
}
