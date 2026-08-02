using MeetingMinutesAI.Domain.Common;

namespace MeetingMinutesAI.Domain.Meetings;

public sealed class MeetingMinutes
{
    private readonly List<MinutesParticipant> _participants = [];
    private readonly List<MinutesTopic> _topics = [];
    private readonly List<MinutesDecision> _decisions = [];
    private readonly List<MinutesActionItem> _actionItems = [];
    private readonly List<MinutesOpenQuestion> _openQuestions = [];
    private readonly List<MinutesUncertainty> _uncertainties = [];

    private MeetingMinutes()
    {
    }

    private MeetingMinutes(
        Guid meetingId,
        Guid? processingRunId,
        MeetingMinutesKind kind,
        int schemaVersion,
        string? title,
        string? dateText,
        string summary,
        DateTimeOffset createdAt)
    {
        if (schemaVersion <= 0)
        {
            throw new DomainRuleException("Minutes schema version must be positive.");
        }

        Id = Guid.NewGuid();
        MeetingId = meetingId;
        ProcessingRunId = processingRunId;
        Kind = kind;
        SchemaVersion = schemaVersion;
        Title = Guard.Optional(title, "Minutes title", 300);
        DateText = Guard.Optional(dateText, "Minutes date", 200);
        Summary = summary ?? string.Empty;
        CreatedAt = Guard.Utc(createdAt);
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public Guid MeetingId { get; private set; }

    public Guid? ProcessingRunId { get; private set; }

    public MeetingMinutesKind Kind { get; private set; }

    public int SchemaVersion { get; private set; }

    public string? Title { get; private set; }

    public string? DateText { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public bool IsLocked { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<MinutesParticipant> Participants => _participants.AsReadOnly();

    public IReadOnlyCollection<MinutesTopic> Topics => _topics.AsReadOnly();

    public IReadOnlyCollection<MinutesDecision> Decisions => _decisions.AsReadOnly();

    public IReadOnlyCollection<MinutesActionItem> ActionItems => _actionItems.AsReadOnly();

    public IReadOnlyCollection<MinutesOpenQuestion> OpenQuestions => _openQuestions.AsReadOnly();

    public IReadOnlyCollection<MinutesUncertainty> Uncertainties => _uncertainties.AsReadOnly();

    public static MeetingMinutes CreateGenerated(
        Guid meetingId,
        Guid processingRunId,
        int schemaVersion,
        string? title,
        string? dateText,
        string summary,
        DateTimeOffset createdAt) =>
        new(
            meetingId,
            processingRunId,
            MeetingMinutesKind.Generated,
            schemaVersion,
            title,
            dateText,
            summary,
            createdAt);

    public void UpdateHeader(
        string? title,
        string? dateText,
        string summary,
        DateTimeOffset updatedAt)
    {
        EnsureEditable();
        Title = Guard.Optional(title, "Minutes title", 300);
        DateText = Guard.Optional(dateText, "Minutes date", 200);
        Summary = summary ?? string.Empty;
        UpdatedAt = Guard.Utc(updatedAt);
    }

    public void AddParticipant(
        string name,
        IEnumerable<RawTranscriptSegment> evidenceSegments)
    {
        EnsureEditable();
        _participants.Add(new MinutesParticipant(
            Id,
            _participants.Count,
            name,
            RequiredEvidence(evidenceSegments)));
    }

    public void AddTopic(
        string title,
        string summary,
        IEnumerable<RawTranscriptSegment> evidenceSegments)
    {
        EnsureEditable();
        _topics.Add(new MinutesTopic(
            Id,
            _topics.Count,
            title,
            summary,
            RequiredEvidence(evidenceSegments)));
    }

    public void AddDecision(
        string text,
        IEnumerable<RawTranscriptSegment> evidenceSegments)
    {
        EnsureEditable();
        _decisions.Add(new MinutesDecision(
            Id,
            _decisions.Count,
            text,
            RequiredEvidence(evidenceSegments)));
    }

    public void AddActionItem(
        string task,
        string? assignee,
        string? deadline,
        IEnumerable<RawTranscriptSegment> evidenceSegments)
    {
        EnsureEditable();
        _actionItems.Add(new MinutesActionItem(
            Id,
            _actionItems.Count,
            task,
            assignee,
            deadline,
            RequiredEvidence(evidenceSegments)));
    }

    public void AddOpenQuestion(
        string text,
        IEnumerable<RawTranscriptSegment> evidenceSegments)
    {
        EnsureEditable();
        _openQuestions.Add(new MinutesOpenQuestion(
            Id,
            _openQuestions.Count,
            text,
            RequiredEvidence(evidenceSegments)));
    }

    public void AddUncertainty(
        string field,
        string description,
        IEnumerable<RawTranscriptSegment>? evidenceSegments = null)
    {
        EnsureEditable();
        _uncertainties.Add(new MinutesUncertainty(
            Id,
            _uncertainties.Count,
            field,
            description,
            DistinctEvidence(evidenceSegments ?? [])));
    }

    internal MeetingMinutes CreateFinalCopy(DateTimeOffset createdAt)
    {
        if (Kind != MeetingMinutesKind.Generated || !IsLocked)
        {
            throw new DomainRuleException("Final minutes require locked generated minutes.");
        }

        var final = new MeetingMinutes(
            MeetingId,
            null,
            MeetingMinutesKind.Final,
            SchemaVersion,
            Title,
            DateText,
            Summary,
            createdAt);

        foreach (var item in _participants)
        {
            final.AddParticipant(item.Name, item.Evidence.Select(link => link.RawSegment));
        }

        foreach (var item in _topics)
        {
            final.AddTopic(item.Title, item.Summary, item.Evidence.Select(link => link.RawSegment));
        }

        foreach (var item in _decisions)
        {
            final.AddDecision(item.Text, item.Evidence.Select(link => link.RawSegment));
        }

        foreach (var item in _actionItems)
        {
            final.AddActionItem(
                item.Task,
                item.Assignee,
                item.Deadline,
                item.Evidence.Select(link => link.RawSegment));
        }

        foreach (var item in _openQuestions)
        {
            final.AddOpenQuestion(item.Text, item.Evidence.Select(link => link.RawSegment));
        }

        foreach (var item in _uncertainties)
        {
            final.AddUncertainty(
                item.Field,
                item.Description,
                item.Evidence.Select(link => link.RawSegment));
        }

        return final;
    }

    internal void LockGenerated()
    {
        if (Kind != MeetingMinutesKind.Generated)
        {
            throw new DomainRuleException("Only generated minutes can be locked.");
        }

        IsLocked = true;
    }

    private IReadOnlyCollection<RawTranscriptSegment> RequiredEvidence(
        IEnumerable<RawTranscriptSegment> evidenceSegments)
    {
        var evidence = DistinctEvidence(evidenceSegments);
        if (Kind == MeetingMinutesKind.Generated && evidence.Count == 0)
        {
            throw new DomainRuleException("Generated minutes item requires evidence.");
        }

        return evidence;
    }

    private static IReadOnlyCollection<RawTranscriptSegment> DistinctEvidence(
        IEnumerable<RawTranscriptSegment> evidenceSegments) =>
        evidenceSegments.DistinctBy(segment => segment.Id).ToList();

    private void EnsureEditable()
    {
        if (IsLocked)
        {
            throw new DomainRuleException("Generated minutes cannot be edited.");
        }
    }
}

public sealed class MinutesParticipant
{
    private readonly List<ParticipantEvidence> _evidence = [];

    private MinutesParticipant()
    {
    }

    internal MinutesParticipant(
        Guid meetingMinutesId,
        int position,
        string name,
        IEnumerable<RawTranscriptSegment> evidence)
    {
        Id = Guid.NewGuid();
        MeetingMinutesId = meetingMinutesId;
        Position = position;
        Name = Guard.Required(name, "Participant name", 300);
        _evidence.AddRange(evidence.Select(segment => new ParticipantEvidence(Id, segment)));
    }

    public Guid Id { get; private set; }
    public Guid MeetingMinutesId { get; private set; }
    public int Position { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public IReadOnlyCollection<ParticipantEvidence> Evidence => _evidence.AsReadOnly();
}

public sealed class MinutesTopic
{
    private readonly List<TopicEvidence> _evidence = [];

    private MinutesTopic()
    {
    }

    internal MinutesTopic(
        Guid meetingMinutesId,
        int position,
        string title,
        string summary,
        IEnumerable<RawTranscriptSegment> evidence)
    {
        Id = Guid.NewGuid();
        MeetingMinutesId = meetingMinutesId;
        Position = position;
        Title = Guard.Required(title, "Topic title", 300);
        Summary = Guard.Required(summary, "Topic summary", int.MaxValue);
        _evidence.AddRange(evidence.Select(segment => new TopicEvidence(Id, segment)));
    }

    public Guid Id { get; private set; }
    public Guid MeetingMinutesId { get; private set; }
    public int Position { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public IReadOnlyCollection<TopicEvidence> Evidence => _evidence.AsReadOnly();
}

public sealed class MinutesDecision
{
    private readonly List<DecisionEvidence> _evidence = [];

    private MinutesDecision()
    {
    }

    internal MinutesDecision(
        Guid meetingMinutesId,
        int position,
        string text,
        IEnumerable<RawTranscriptSegment> evidence)
    {
        Id = Guid.NewGuid();
        MeetingMinutesId = meetingMinutesId;
        Position = position;
        Text = Guard.Required(text, "Decision text", int.MaxValue);
        _evidence.AddRange(evidence.Select(segment => new DecisionEvidence(Id, segment)));
    }

    public Guid Id { get; private set; }
    public Guid MeetingMinutesId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public IReadOnlyCollection<DecisionEvidence> Evidence => _evidence.AsReadOnly();
}

public sealed class MinutesActionItem
{
    private readonly List<ActionItemEvidence> _evidence = [];

    private MinutesActionItem()
    {
    }

    internal MinutesActionItem(
        Guid meetingMinutesId,
        int position,
        string task,
        string? assignee,
        string? deadline,
        IEnumerable<RawTranscriptSegment> evidence)
    {
        Id = Guid.NewGuid();
        MeetingMinutesId = meetingMinutesId;
        Position = position;
        Task = Guard.Required(task, "Action task", int.MaxValue);
        Assignee = Guard.Optional(assignee, "Action assignee", 300);
        Deadline = Guard.Optional(deadline, "Action deadline", 200);
        _evidence.AddRange(evidence.Select(segment => new ActionItemEvidence(Id, segment)));
    }

    public Guid Id { get; private set; }
    public Guid MeetingMinutesId { get; private set; }
    public int Position { get; private set; }
    public string Task { get; private set; } = string.Empty;
    public string? Assignee { get; private set; }
    public string? Deadline { get; private set; }
    public IReadOnlyCollection<ActionItemEvidence> Evidence => _evidence.AsReadOnly();
}

public sealed class MinutesOpenQuestion
{
    private readonly List<OpenQuestionEvidence> _evidence = [];

    private MinutesOpenQuestion()
    {
    }

    internal MinutesOpenQuestion(
        Guid meetingMinutesId,
        int position,
        string text,
        IEnumerable<RawTranscriptSegment> evidence)
    {
        Id = Guid.NewGuid();
        MeetingMinutesId = meetingMinutesId;
        Position = position;
        Text = Guard.Required(text, "Open question", int.MaxValue);
        _evidence.AddRange(evidence.Select(segment => new OpenQuestionEvidence(Id, segment)));
    }

    public Guid Id { get; private set; }
    public Guid MeetingMinutesId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public IReadOnlyCollection<OpenQuestionEvidence> Evidence => _evidence.AsReadOnly();
}

public sealed class MinutesUncertainty
{
    private readonly List<UncertaintyEvidence> _evidence = [];

    private MinutesUncertainty()
    {
    }

    internal MinutesUncertainty(
        Guid meetingMinutesId,
        int position,
        string field,
        string description,
        IEnumerable<RawTranscriptSegment> evidence)
    {
        Id = Guid.NewGuid();
        MeetingMinutesId = meetingMinutesId;
        Position = position;
        Field = Guard.Required(field, "Uncertainty field", 200);
        Description = Guard.Required(description, "Uncertainty description", int.MaxValue);
        _evidence.AddRange(evidence.Select(segment => new UncertaintyEvidence(Id, segment)));
    }

    public Guid Id { get; private set; }
    public Guid MeetingMinutesId { get; private set; }
    public int Position { get; private set; }
    public string Field { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public IReadOnlyCollection<UncertaintyEvidence> Evidence => _evidence.AsReadOnly();
}
