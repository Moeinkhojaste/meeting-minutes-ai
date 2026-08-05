using MeetingMinutesAI.Domain.Common;
using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Domain.Tests;

public sealed class MeetingAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MeetingFollowsTheCompletedProcessingFlow()
    {
        var state = CreateGeneratingMeeting();
        var generated = CreateGeneratedMinutes(state.Meeting, state.Run);

        state.Meeting.AttachGeneratedMinutes(state.Run, generated, Now.AddSeconds(5));
        state.Run.Complete(Now.AddSeconds(6), 6000);
        state.Meeting.Complete(state.Run, Now.AddSeconds(6));

        Assert.Equal(MeetingProcessingStatus.Completed, state.Meeting.Status);
        Assert.True(generated.IsLocked);
        Assert.Single(state.Meeting.Minutes);
    }

    [Fact]
    public void PartialCompletionPreservesRawTranscriptWithoutDerivedOutput()
    {
        var meeting = CreateQueuedMeeting();
        var run = meeting.StartTranscription(ProcessingMode.Fast, "correlation-partial", Now);
        meeting.SetRawTranscript(run, 1, [CreateRawSegment()], Now.AddSeconds(1));
        meeting.StartMinutesGeneration(run, Now.AddSeconds(2));
        run.CompletePartially("MINUTES_FAILED", "Minutes generation failed.", true, Now.AddSeconds(3), 3000);

        meeting.CompletePartially(
            run,
            "MINUTES_FAILED",
            "Minutes generation failed.",
            Now.AddSeconds(3));

        Assert.Equal(MeetingProcessingStatus.PartiallyCompleted, meeting.Status);
        Assert.NotNull(meeting.RawTranscript);
        Assert.Null(meeting.CleanedTranscript);
        Assert.Empty(meeting.Minutes);
    }

    [Fact]
    public void InvalidStatusTransitionUsesContentFreeError()
    {
        var meeting = Meeting.Create("private-title", Now);

        var exception = Assert.Throws<DomainRuleException>(() => meeting.Queue(Now));

        Assert.DoesNotContain("private-title", exception.Message, StringComparison.Ordinal);
        Assert.Equal(MeetingProcessingStatus.Created, meeting.Status);
    }

    [Fact]
    public void RawTranscriptRejectsDuplicateExternalIds()
    {
        var meeting = CreateQueuedMeeting();
        var run = meeting.StartTranscription(ProcessingMode.Fast, "correlation-duplicate", Now);
        var duplicate = new[]
        {
            CreateRawSegment("seg-0001", 0),
            CreateRawSegment("seg-0001", 1),
        };

        Assert.Throws<DomainRuleException>(() =>
            meeting.SetRawTranscript(run, 1, duplicate, Now.AddSeconds(1)));
    }

    [Fact]
    public void RawSegmentRejectsInvalidTimestampRange()
    {
        Assert.Throws<DomainRuleException>(() => RawTranscriptSegment.Create(
            "seg-0001",
            0,
            null,
            100,
            100,
            "fa",
            "private transcript content"));
    }

    [Fact]
    public void GeneratedFactRequiresEvidenceAndLocksAfterAttachment()
    {
        var state = CreateGeneratingMeeting();
        var generated = MeetingMinutes.CreateGenerated(
            state.Meeting.Id,
            state.Run.Id,
            1,
            null,
            null,
            "summary",
            Now);

        Assert.Throws<DomainRuleException>(() =>
            generated.AddDecision("decision", []));

        generated.AddDecision("decision", state.Meeting.RawTranscript!.Segments);
        state.Meeting.AttachGeneratedMinutes(state.Run, generated, Now.AddSeconds(5));

        Assert.Throws<DomainRuleException>(() =>
            generated.UpdateHeader(null, null, "changed", Now.AddSeconds(6)));
    }

    [Fact]
    public void FinalMinutesAreASeparateEditableCopy()
    {
        var state = CreateGeneratingMeeting();
        var generated = CreateGeneratedMinutes(state.Meeting, state.Run);
        state.Meeting.AttachGeneratedMinutes(state.Run, generated, Now.AddSeconds(5));

        var final = state.Meeting.CreateFinalMinutes(Now.AddSeconds(6));
        final.UpdateHeader("Edited", null, "Edited summary", Now.AddSeconds(7));

        Assert.Equal(MeetingMinutesKind.Final, final.Kind);
        Assert.Equal("Edited", final.Title);
        Assert.NotEqual(final.Id, generated.Id);
        Assert.Equal("Generated", generated.Title);
    }

    [Fact]
    public void RequeueClearsCurrentOutputButRetainsAttemptHistory()
    {
        var state = CreateGeneratingMeeting();
        var generated = CreateGeneratedMinutes(state.Meeting, state.Run);
        state.Meeting.AttachGeneratedMinutes(state.Run, generated, Now.AddSeconds(5));
        state.Run.Complete(Now.AddSeconds(6), 6000);
        state.Meeting.Complete(state.Run, Now.AddSeconds(6));

        state.Meeting.Queue(Now.AddSeconds(7));

        Assert.Equal(MeetingProcessingStatus.Queued, state.Meeting.Status);
        Assert.Null(state.Meeting.RawTranscript);
        Assert.Null(state.Meeting.CleanedTranscript);
        Assert.Empty(state.Meeting.Minutes);
        Assert.Single(state.Meeting.ProcessingRuns);
    }

    private static (Meeting Meeting, ProcessingRun Run) CreateGeneratingMeeting()
    {
        var meeting = CreateQueuedMeeting();
        var run = meeting.StartTranscription(ProcessingMode.Quality, "correlation-complete", Now);
        meeting.SetRawTranscript(run, 1, [CreateRawSegment()], Now.AddSeconds(1));
        meeting.StartMinutesGeneration(run, Now.AddSeconds(2));
        var raw = meeting.RawTranscript!.Segments.Single();
        meeting.SetCleanedTranscript(
            run,
            1,
            [CleanedTranscriptSegment.Create("clean-0001", 0, "cleaned", [raw])],
            Now.AddSeconds(3));
        return (meeting, run);
    }

    private static MeetingMinutes CreateGeneratedMinutes(Meeting meeting, ProcessingRun run)
    {
        var generated = MeetingMinutes.CreateGenerated(
            meeting.Id,
            run.Id,
            1,
            "Generated",
            null,
            "Summary",
            Now.AddSeconds(4));
        generated.AddParticipant("Participant", meeting.RawTranscript!.Segments);
        generated.AddUncertainty("date", "Date was not stated.");
        return generated;
    }

    private static Meeting CreateQueuedMeeting()
    {
        var meeting = Meeting.Create("Meeting", Now);
        meeting.AttachAudio(
            "meeting.wav",
            "audio/opaque-key",
            "audio/wav",
            1024,
            1000,
            new string('a', 64),
            Now);
        meeting.Queue(Now);
        return meeting;
    }

    private static RawTranscriptSegment CreateRawSegment(
        string id = "seg-0001",
        int position = 0) =>
        RawTranscriptSegment.Create(id, position, null, 0, 1000, "fa", "text");
}
