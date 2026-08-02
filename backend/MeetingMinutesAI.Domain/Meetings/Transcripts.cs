using MeetingMinutesAI.Domain.Common;

namespace MeetingMinutesAI.Domain.Meetings;

public sealed class RawTranscript
{
    private readonly List<RawTranscriptSegment> _segments = [];

    private RawTranscript()
    {
    }

    private RawTranscript(
        Guid meetingId,
        Guid processingRunId,
        int schemaVersion,
        IEnumerable<RawTranscriptSegment> segments,
        DateTimeOffset createdAt)
    {
        if (schemaVersion <= 0)
        {
            throw new DomainRuleException("Transcript schema version must be positive.");
        }

        var materialized = segments.OrderBy(segment => segment.Position).ToList();
        EnsureUniqueSegments(materialized);

        Id = Guid.NewGuid();
        MeetingId = meetingId;
        ProcessingRunId = processingRunId;
        SchemaVersion = schemaVersion;
        CreatedAt = Guard.Utc(createdAt);
        foreach (var segment in materialized)
        {
            segment.AttachToTranscript(Id);
            _segments.Add(segment);
        }
    }

    public Guid Id { get; private set; }

    public Guid MeetingId { get; private set; }

    public Guid ProcessingRunId { get; private set; }

    public int SchemaVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<RawTranscriptSegment> Segments => _segments.AsReadOnly();

    internal static RawTranscript Create(
        Guid meetingId,
        Guid processingRunId,
        int schemaVersion,
        IEnumerable<RawTranscriptSegment> segments,
        DateTimeOffset createdAt) =>
        new(meetingId, processingRunId, schemaVersion, segments, createdAt);

    private static void EnsureUniqueSegments(IReadOnlyCollection<RawTranscriptSegment> segments)
    {
        if (segments.Select(segment => segment.ExternalId).Distinct(StringComparer.Ordinal).Count()
            != segments.Count)
        {
            throw new DomainRuleException("Raw transcript segment IDs must be unique.");
        }

        if (segments.Select(segment => segment.Position).Distinct().Count() != segments.Count)
        {
            throw new DomainRuleException("Raw transcript segment positions must be unique.");
        }
    }
}

public sealed class RawTranscriptSegment
{
    private RawTranscriptSegment()
    {
    }

    private RawTranscriptSegment(
        string externalId,
        int position,
        string? speaker,
        long startMilliseconds,
        long endMilliseconds,
        string language,
        string text)
    {
        if (position < 0)
        {
            throw new DomainRuleException("Segment position cannot be negative.");
        }

        if (startMilliseconds < 0 || endMilliseconds <= startMilliseconds)
        {
            throw new DomainRuleException("Segment timestamps are invalid.");
        }

        Id = Guid.NewGuid();
        ExternalId = Guard.Required(externalId, "Raw segment ID", 64);
        Position = position;
        Speaker = Guard.Optional(speaker, "Speaker", 200);
        StartMilliseconds = startMilliseconds;
        EndMilliseconds = endMilliseconds;
        Language = Guard.Required(language, "Language", 32);
        Text = Guard.Required(text, "Raw segment text", int.MaxValue);
    }

    public Guid Id { get; private set; }

    public Guid RawTranscriptId { get; private set; }

    public string ExternalId { get; private set; } = string.Empty;

    public int Position { get; private set; }

    public string? Speaker { get; private set; }

    public long StartMilliseconds { get; private set; }

    public long EndMilliseconds { get; private set; }

    public string Language { get; private set; } = string.Empty;

    public string Text { get; private set; } = string.Empty;

    public static RawTranscriptSegment Create(
        string externalId,
        int position,
        string? speaker,
        long startMilliseconds,
        long endMilliseconds,
        string language,
        string text) =>
        new(
            externalId,
            position,
            speaker,
            startMilliseconds,
            endMilliseconds,
            language,
            text);

    internal void AttachToTranscript(Guid transcriptId)
    {
        if (RawTranscriptId != Guid.Empty)
        {
            throw new DomainRuleException("Raw segment is already attached.");
        }

        RawTranscriptId = transcriptId;
    }
}

public sealed class CleanedTranscript
{
    private readonly List<CleanedTranscriptSegment> _segments = [];

    private CleanedTranscript()
    {
    }

    private CleanedTranscript(
        Guid meetingId,
        Guid processingRunId,
        int schemaVersion,
        IEnumerable<CleanedTranscriptSegment> segments,
        DateTimeOffset createdAt)
    {
        if (schemaVersion <= 0)
        {
            throw new DomainRuleException("Transcript schema version must be positive.");
        }

        var materialized = segments.OrderBy(segment => segment.Position).ToList();
        if (materialized.Select(segment => segment.ExternalId)
                .Distinct(StringComparer.Ordinal).Count() != materialized.Count
            || materialized.Select(segment => segment.Position).Distinct().Count()
                != materialized.Count)
        {
            throw new DomainRuleException("Cleaned transcript segments must be uniquely ordered.");
        }

        Id = Guid.NewGuid();
        MeetingId = meetingId;
        ProcessingRunId = processingRunId;
        SchemaVersion = schemaVersion;
        CreatedAt = Guard.Utc(createdAt);
        foreach (var segment in materialized)
        {
            segment.AttachToTranscript(Id);
            _segments.Add(segment);
        }
    }

    public Guid Id { get; private set; }

    public Guid MeetingId { get; private set; }

    public Guid ProcessingRunId { get; private set; }

    public int SchemaVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<CleanedTranscriptSegment> Segments => _segments.AsReadOnly();

    internal static CleanedTranscript Create(
        Guid meetingId,
        Guid processingRunId,
        int schemaVersion,
        IEnumerable<CleanedTranscriptSegment> segments,
        DateTimeOffset createdAt) =>
        new(meetingId, processingRunId, schemaVersion, segments, createdAt);
}

public sealed class CleanedTranscriptSegment
{
    private readonly List<CleanedSegmentSource> _sources = [];

    private CleanedTranscriptSegment()
    {
    }

    private CleanedTranscriptSegment(
        string externalId,
        int position,
        string text,
        IEnumerable<RawTranscriptSegment> sourceSegments)
    {
        if (position < 0)
        {
            throw new DomainRuleException("Segment position cannot be negative.");
        }

        var sources = sourceSegments.DistinctBy(segment => segment.Id).ToList();
        if (sources.Count == 0)
        {
            throw new DomainRuleException("A cleaned segment requires a raw source.");
        }

        Id = Guid.NewGuid();
        ExternalId = Guard.Required(externalId, "Cleaned segment ID", 64);
        Position = position;
        Text = Guard.Required(text, "Cleaned segment text", int.MaxValue);
        _sources.AddRange(sources.Select(source => new CleanedSegmentSource(Id, source)));
    }

    public Guid Id { get; private set; }

    public Guid CleanedTranscriptId { get; private set; }

    public string ExternalId { get; private set; } = string.Empty;

    public int Position { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public IReadOnlyCollection<CleanedSegmentSource> Sources => _sources.AsReadOnly();

    public static CleanedTranscriptSegment Create(
        string externalId,
        int position,
        string text,
        IEnumerable<RawTranscriptSegment> sourceSegments) =>
        new(externalId, position, text, sourceSegments);

    internal void AttachToTranscript(Guid transcriptId)
    {
        if (CleanedTranscriptId != Guid.Empty)
        {
            throw new DomainRuleException("Cleaned segment is already attached.");
        }

        CleanedTranscriptId = transcriptId;
    }
}

public sealed class CleanedSegmentSource
{
    private CleanedSegmentSource()
    {
    }

    internal CleanedSegmentSource(Guid cleanedSegmentId, RawTranscriptSegment rawSegment)
    {
        CleanedTranscriptSegmentId = cleanedSegmentId;
        RawTranscriptSegmentId = rawSegment.Id;
        RawSegment = rawSegment;
    }

    public Guid CleanedTranscriptSegmentId { get; private set; }

    public Guid RawTranscriptSegmentId { get; private set; }

    public RawTranscriptSegment RawSegment { get; private set; } = null!;
}
