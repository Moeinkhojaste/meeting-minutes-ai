using MeetingMinutesAI.Domain.Common;
using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Domain.Tests;

public sealed class MeetingStatusTransitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FullHappyPathStatusTransitionsSucceed()
    {
        // Created -> Uploaded
        var meeting = Meeting.Create("Planning", Now, "user-123");
        Assert.Equal(MeetingProcessingStatus.Created, meeting.Status);

        meeting.AttachAudio("test.wav", "key-1", "audio/wav", 100, 500, new string('a', 64), Now);
        Assert.Equal(MeetingProcessingStatus.Uploaded, meeting.Status);

        // Uploaded -> Queued
        meeting.Queue(Now.AddSeconds(1));
        Assert.Equal(MeetingProcessingStatus.Queued, meeting.Status);

        // Queued -> Transcribing
        var run = meeting.StartTranscription(ProcessingMode.Quality, "corr-1", Now.AddSeconds(2));
        Assert.Equal(MeetingProcessingStatus.Transcribing, meeting.Status);

        // Transcribing -> RawTranscript set
        meeting.SetRawTranscript(run, 1, [CreateRawSegment()], Now.AddSeconds(3));
        Assert.NotNull(meeting.RawTranscript);

        // Transcribing -> GeneratingMinutes
        meeting.StartMinutesGeneration(run, Now.AddSeconds(4));
        Assert.Equal(MeetingProcessingStatus.GeneratingMinutes, meeting.Status);

        // GeneratingMinutes -> CleanedTranscript set & Minutes attached
        var rawSeg = meeting.RawTranscript!.Segments.Single();
        meeting.SetCleanedTranscript(run, 1, [CleanedTranscriptSegment.Create("clean-1", 0, "Cleaned", [rawSeg])], Now.AddSeconds(5));

        var minutes = MeetingMinutes.CreateGenerated(meeting.Id, run.Id, 1, "Generated Minutes", null, "Summary", Now.AddSeconds(6));
        minutes.AddParticipant("Participant A", meeting.RawTranscript!.Segments);
        meeting.AttachGeneratedMinutes(run, minutes, Now.AddSeconds(7));

        run.Complete(Now.AddSeconds(8), 5000);
        meeting.Complete(run, Now.AddSeconds(8));

        Assert.Equal(MeetingProcessingStatus.Completed, meeting.Status);
        Assert.Null(meeting.ProcessingErrorCode);
        Assert.Null(meeting.ProcessingErrorMessage);
    }

    [Fact]
    public void StageTwoFailurePreservesRawTranscriptAndSetsPartiallyCompleted()
    {
        var meeting = CreateGeneratingMinutesMeeting();
        var run = meeting.ProcessingRuns.Last();

        // Complete run partially
        run.CompletePartially("STAGE_TWO_ERROR", "LLM minute generation failed", true, Now.AddSeconds(10), 4000);

        // Complete meeting partially
        meeting.CompletePartially(run, "STAGE_TWO_ERROR", "LLM minute generation failed", Now.AddSeconds(10));

        Assert.Equal(MeetingProcessingStatus.PartiallyCompleted, meeting.Status);
        Assert.NotNull(meeting.RawTranscript);
        Assert.Null(meeting.CleanedTranscript);
        Assert.Empty(meeting.Minutes);
        Assert.Equal("STAGE_TWO_ERROR", meeting.ProcessingErrorCode);
        Assert.Equal("LLM minute generation failed", meeting.ProcessingErrorMessage);
    }

    [Fact]
    public void RequeueFromPartiallyCompletedClearsOutputAndResetsStatusToQueued()
    {
        var meeting = CreateGeneratingMinutesMeeting();
        var run = meeting.ProcessingRuns.Last();
        run.CompletePartially("STAGE_TWO_ERROR", "Failed", true, Now.AddSeconds(10), 4000);
        meeting.CompletePartially(run, "STAGE_TWO_ERROR", "Failed", Now.AddSeconds(10));

        // Requeue
        meeting.Queue(Now.AddSeconds(15));

        Assert.Equal(MeetingProcessingStatus.Queued, meeting.Status);
        Assert.Null(meeting.RawTranscript);
        Assert.Null(meeting.CleanedTranscript);
        Assert.Empty(meeting.Minutes);
        Assert.Null(meeting.ProcessingErrorCode);
        Assert.Null(meeting.ProcessingErrorMessage);
        Assert.Single(meeting.ProcessingRuns); // Attempt history preserved
    }

    [Fact]
    public void RequeueFromFailedResetsStatusToQueued()
    {
        var meeting = CreateQueuedMeeting();
        var run = meeting.StartTranscription(ProcessingMode.Fast, "corr-fail", Now);
        run.Fail("STT_FAIL", "Speech to text failed", true, Now.AddSeconds(1), 1000);
        meeting.Fail(run, "STT_FAIL", "Speech to text failed", Now.AddSeconds(1));

        Assert.Equal(MeetingProcessingStatus.Failed, meeting.Status);

        meeting.Queue(Now.AddSeconds(5));
        Assert.Equal(MeetingProcessingStatus.Queued, meeting.Status);
        Assert.Null(meeting.ProcessingErrorCode);
        Assert.Single(meeting.ProcessingRuns);
    }

    [Fact]
    public void InvalidTransitionFromCreatedToQueuedThrowsDomainRuleException()
    {
        var meeting = Meeting.Create("Unuploaded", Now);

        var ex = Assert.Throws<DomainRuleException>(() => meeting.Queue(Now));
        Assert.Equal("Meeting cannot be queued from its current status.", ex.Message);
    }

    [Fact]
    public void InvalidTransitionFromQueuedToCompletedThrowsDomainRuleException()
    {
        var meeting = CreateQueuedMeeting();
        var run = meeting.StartTranscription(ProcessingMode.Fast, "corr-invalid", Now);

        var ex = Assert.Throws<DomainRuleException>(() => meeting.Complete(run, Now));
        Assert.Equal("Meeting is not in the required processing status.", ex.Message);
    }

    [Fact]
    public void EnsureOwnerThrowsDomainRuleExceptionOnMismatch()
    {
        var meeting = Meeting.Create("Owned Meeting", Now, "owner-id-1");

        meeting.EnsureOwner("owner-id-1"); // Should not throw

        var ex = Assert.Throws<DomainRuleException>(() => meeting.EnsureOwner("other-user-id"));
        Assert.Equal("Access to this meeting is forbidden.", ex.Message);
    }

    private static Meeting CreateQueuedMeeting()
    {
        var meeting = Meeting.Create("Queued Meeting", Now, "owner-1");
        meeting.AttachAudio("audio.wav", "storage/key", "audio/wav", 1000, 2000, new string('b', 64), Now);
        meeting.Queue(Now);
        return meeting;
    }

    private static Meeting CreateGeneratingMinutesMeeting()
    {
        var meeting = CreateQueuedMeeting();
        var run = meeting.StartTranscription(ProcessingMode.Quality, "corr-stage2", Now);
        meeting.SetRawTranscript(run, 1, [CreateRawSegment()], Now.AddSeconds(1));
        meeting.StartMinutesGeneration(run, Now.AddSeconds(2));
        return meeting;
    }

    private static RawTranscriptSegment CreateRawSegment() =>
        RawTranscriptSegment.Create("seg-1", 0, null, 0, 1000, "fa", "متن صورت‌جلسه");
}
