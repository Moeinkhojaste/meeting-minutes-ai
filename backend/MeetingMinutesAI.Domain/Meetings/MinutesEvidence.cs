namespace MeetingMinutesAI.Domain.Meetings;

public sealed class ParticipantEvidence
{
    private ParticipantEvidence() { }
    internal ParticipantEvidence(Guid itemId, RawTranscriptSegment segment)
    {
        MinutesParticipantId = itemId;
        RawTranscriptSegmentId = segment.Id;
        RawSegment = segment;
    }

    public Guid MinutesParticipantId { get; private set; }
    public Guid RawTranscriptSegmentId { get; private set; }
    public RawTranscriptSegment RawSegment { get; private set; } = null!;
}

public sealed class TopicEvidence
{
    private TopicEvidence() { }
    internal TopicEvidence(Guid itemId, RawTranscriptSegment segment)
    {
        MinutesTopicId = itemId;
        RawTranscriptSegmentId = segment.Id;
        RawSegment = segment;
    }

    public Guid MinutesTopicId { get; private set; }
    public Guid RawTranscriptSegmentId { get; private set; }
    public RawTranscriptSegment RawSegment { get; private set; } = null!;
}

public sealed class DecisionEvidence
{
    private DecisionEvidence() { }
    internal DecisionEvidence(Guid itemId, RawTranscriptSegment segment)
    {
        MinutesDecisionId = itemId;
        RawTranscriptSegmentId = segment.Id;
        RawSegment = segment;
    }

    public Guid MinutesDecisionId { get; private set; }
    public Guid RawTranscriptSegmentId { get; private set; }
    public RawTranscriptSegment RawSegment { get; private set; } = null!;
}

public sealed class ActionItemEvidence
{
    private ActionItemEvidence() { }
    internal ActionItemEvidence(Guid itemId, RawTranscriptSegment segment)
    {
        MinutesActionItemId = itemId;
        RawTranscriptSegmentId = segment.Id;
        RawSegment = segment;
    }

    public Guid MinutesActionItemId { get; private set; }
    public Guid RawTranscriptSegmentId { get; private set; }
    public RawTranscriptSegment RawSegment { get; private set; } = null!;
}

public sealed class OpenQuestionEvidence
{
    private OpenQuestionEvidence() { }
    internal OpenQuestionEvidence(Guid itemId, RawTranscriptSegment segment)
    {
        MinutesOpenQuestionId = itemId;
        RawTranscriptSegmentId = segment.Id;
        RawSegment = segment;
    }

    public Guid MinutesOpenQuestionId { get; private set; }
    public Guid RawTranscriptSegmentId { get; private set; }
    public RawTranscriptSegment RawSegment { get; private set; } = null!;
}

public sealed class UncertaintyEvidence
{
    private UncertaintyEvidence() { }
    internal UncertaintyEvidence(Guid itemId, RawTranscriptSegment segment)
    {
        MinutesUncertaintyId = itemId;
        RawTranscriptSegmentId = segment.Id;
        RawSegment = segment;
    }

    public Guid MinutesUncertaintyId { get; private set; }
    public Guid RawTranscriptSegmentId { get; private set; }
    public RawTranscriptSegment RawSegment { get; private set; } = null!;
}
