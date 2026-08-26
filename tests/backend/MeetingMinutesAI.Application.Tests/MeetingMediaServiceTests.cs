using MeetingMinutesAI.Application.Abstractions.Ai;
using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Application.Abstractions.Storage;
using MeetingMinutesAI.Application.Meetings;
using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Tests;

public sealed class MeetingMediaServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UploadStoresValidatedMetadataAndSanitizesDisplayName()
    {
        var meeting = Meeting.Create("Planning", Now);
        var state = CreateState(meeting);

        var result = await state.Service.UploadAudioAsync(
            meeting.Id,
            "../../private/meeting.wav",
            new MemoryStream([1, 2, 3]),
            []);

        Assert.Equal(MeetingProcessingStatus.Uploaded, result.Status);
        Assert.Equal("meeting.wav", result.Audio!.OriginalFileName);
        Assert.Equal("audio/wav", result.Audio.ContentType);
        Assert.Equal(3, result.Audio.ByteLength);
        Assert.Equal(1, state.UnitOfWork.SaveCount);
        Assert.DoesNotContain("objects/", result.Audio.OriginalFileName);
    }

    [Fact]
    public async Task UploadCompensatesStoredObjectWhenDatabaseSaveFails()
    {
        var meeting = Meeting.Create(null, Now);
        var state = CreateState(meeting);
        state.UnitOfWork.Failure = new InvalidOperationException("database failed");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            state.Service.UploadAudioAsync(
                meeting.Id, "meeting.wav", new MemoryStream([1]), []));

        Assert.True(state.Storage.DeleteCalled);
    }

    [Fact]
    public async Task UploadRejectsStaleVersionAndDuplicateWithoutWritingStorage()
    {
        var meeting = UploadedMeeting();
        var state = CreateState(meeting);

        await Assert.ThrowsAsync<MeetingConcurrencyException>(() =>
            state.Service.UploadAudioAsync(
                meeting.Id, "meeting.wav", new MemoryStream([1]), [1]));
        await Assert.ThrowsAsync<MeetingCommandConflictException>(() =>
            state.Service.UploadAudioAsync(
                meeting.Id, "meeting.wav", new MemoryStream([1]), []));

        Assert.Equal(0, state.Storage.SaveCount);
    }

    [Fact]
    public async Task SuccessfulProcessingPersistsRawBeforeStageTwoAndMapsEvidence()
    {
        var meeting = UploadedMeeting();
        var state = CreateState(meeting);
        state.Ai.BeforeMinutes = () =>
        {
            Assert.Equal(3, state.UnitOfWork.SaveCount);
            Assert.Equal(MeetingProcessingStatus.GeneratingMinutes, meeting.Status);
            Assert.NotNull(meeting.RawTranscript);
        };

        var result = await state.Service.ProcessAsync(
            meeting.Id, ProcessingMode.Fast, [], "corr-1");

        Assert.Equal(MeetingProcessingStatus.Completed, result.Status);
        Assert.NotNull(meeting.RawTranscript);
        Assert.NotNull(meeting.CleanedTranscript);
        var minutes = meeting.Minutes.Single();
        Assert.True(minutes.IsLocked);
        Assert.Equal(
            "seg-0001",
            minutes.Decisions.Single().Evidence.Single().RawSegment.ExternalId);
        Assert.Equal(
            [MeetingProcessingStatus.Queued, MeetingProcessingStatus.Transcribing,
                MeetingProcessingStatus.GeneratingMinutes, MeetingProcessingStatus.Completed],
            state.UnitOfWork.SavedStatuses);
    }

    [Fact]
    public async Task StageTwoFailureKeepsRawAndReturnsPartialWithoutOtherOutputs()
    {
        var meeting = UploadedMeeting();
        var state = CreateState(meeting);
        state.Ai.MinutesFailure = new AiServiceException(
            AiFailureKind.Unavailable,
            "GEMINI_UNAVAILABLE",
            "Minutes generation is unavailable.",
            true);

        var result = await state.Service.ProcessAsync(
            meeting.Id, ProcessingMode.Fast, [], "corr-1");

        Assert.Equal(MeetingProcessingStatus.PartiallyCompleted, result.Status);
        Assert.NotNull(meeting.RawTranscript);
        Assert.Null(meeting.CleanedTranscript);
        Assert.Empty(meeting.Minutes);
        Assert.Equal("GEMINI_UNAVAILABLE", result.ProcessingErrorCode);
        Assert.Equal(ProcessingRunStatus.PartiallyCompleted,
            meeting.ProcessingRuns.Single().Status);
    }

    [Fact]
    public async Task StageOneFailureIsPersistedAndSurfacedAsProcessingFailure()
    {
        var meeting = UploadedMeeting();
        var state = CreateState(meeting);
        state.Ai.TranscriptionFailure = new AiServiceException(
            AiFailureKind.InvalidRequest,
            "AUDIO_REJECTED",
            "Audio was rejected.",
            false);

        var exception = await Assert.ThrowsAsync<MeetingProcessingException>(() =>
            state.Service.ProcessAsync(
                meeting.Id, ProcessingMode.Quality, [], "corr-1"));

        Assert.Equal("AUDIO_REJECTED", exception.Failure.Code);
        Assert.Equal(MeetingProcessingStatus.Failed, meeting.Status);
        Assert.Null(meeting.RawTranscript);
        Assert.Equal(ProcessingRunStatus.Failed, meeting.ProcessingRuns.Single().Status);
    }

    [Fact]
    public async Task InvalidStageTwoEvidenceBecomesPartialResult()
    {
        var meeting = UploadedMeeting();
        var state = CreateState(meeting);
        state.Ai.MinutesResult = ValidMinutes(
            evidenceId: "seg-9999");

        var result = await state.Service.ProcessAsync(
            meeting.Id, ProcessingMode.Fast, [], "corr-1");

        Assert.Equal(MeetingProcessingStatus.PartiallyCompleted, result.Status);
        Assert.Equal("AI_INVALID_RESPONSE", result.ProcessingErrorCode);
        Assert.NotNull(meeting.RawTranscript);
        Assert.Null(meeting.CleanedTranscript);
    }

    [Fact]
    public async Task ExplicitRerunRetainsAttemptHistoryAndReplacesOutputs()
    {
        var meeting = UploadedMeeting();
        var state = CreateState(meeting);
        await state.Service.ProcessAsync(
            meeting.Id, ProcessingMode.Fast, [], "corr-1");

        state.Ai.Mode = ProcessingMode.Quality;
        state.Ai.CorrelationId = "corr-2";
        await state.Service.ProcessAsync(
            meeting.Id, ProcessingMode.Quality, [], "corr-2");

        Assert.Equal(2, meeting.ProcessingRuns.Count);
        Assert.Equal([1, 2], meeting.ProcessingRuns.Select(run => run.AttemptNumber));
        Assert.Single(meeting.Minutes);
        Assert.Equal(ProcessingMode.Quality, meeting.ProcessingRuns.Last().RequestedMode);
    }

    [Fact]
    public async Task ProcessAsync_WhenTranscriptionUsesFasterWhisperFallback_Succeeds()
    {
        var meeting = UploadedMeeting();
        var state = CreateState(meeting);
        state.Ai.TranscriptionResult = new AiTranscriptionResult(
            new AiRawTranscript(1,
                [new AiRawSegment("seg-0001", "Speaker", 0, 1000, "fa", "متن")]),
            Metadata("transcription", ProcessingMode.Fast, actualProvider: "faster-whisper", actualModel: "small", fallbackUsed: true),
            "corr-1");

        var result = await state.Service.ProcessAsync(
            meeting.Id, ProcessingMode.Fast, [], "corr-1");

        Assert.Equal(MeetingProcessingStatus.Completed, result.Status);
        Assert.True(meeting.ProcessingRuns.Single().Stages.First().FallbackUsed);
        Assert.Equal("faster-whisper", meeting.ProcessingRuns.Single().Stages.First().ActualProvider);
    }

    [Fact]
    public async Task ProcessAsync_WhenMinutesUsesLocalLlmFallback_Succeeds()
    {
        var meeting = UploadedMeeting();
        var state = CreateState(meeting);
        state.Ai.MinutesResult = new AiMinutesResult(
            new AiCleanedTranscript(1,
                [new AiCleanedSegment("clean-0001", "متن", ["seg-0001"])]),
            new AiGeneratedMinutes(
                1, null, null, "خلاصه", [], [],
                [new AiDecision("تصمیم", ["seg-0001"])], [], [], []),
            Metadata("minutes", ProcessingMode.Fast, actualProvider: "local-llm", actualModel: "qwen2.5:3b-instruct", fallbackUsed: true),
            "corr-1");

        var result = await state.Service.ProcessAsync(
            meeting.Id, ProcessingMode.Fast, [], "corr-1");

        Assert.Equal(MeetingProcessingStatus.Completed, result.Status);
        Assert.True(meeting.ProcessingRuns.Single().Stages.Last().FallbackUsed);
        Assert.Equal("local-llm", meeting.ProcessingRuns.Single().Stages.Last().ActualProvider);
    }

    private static TestState CreateState(Meeting meeting)
    {
        var repository = new FakeRepository(meeting);
        var unitOfWork = new RecordingUnitOfWork(meeting);
        var storage = new FakeStorage();
        var ai = new FakeAiClient();
        var service = new MeetingMediaService(
            repository,
            unitOfWork,
            storage,
            ai,
            new FixedTimeProvider(Now));
        return new TestState(service, unitOfWork, storage, ai);
    }

    private static Meeting UploadedMeeting()
    {
        var meeting = Meeting.Create("Planning", Now);
        meeting.AttachAudio(
            "meeting.wav", "objects/00000000000000000000000000000000.wav",
            "audio/wav", 16, null, new string('a', 64), Now);
        return meeting;
    }

    private static AiTranscriptionResult ValidTranscription(
        ProcessingMode mode = ProcessingMode.Fast,
        string correlationId = "corr-1") => new(
            new AiRawTranscript(1,
                [new AiRawSegment("seg-0001", "Speaker", 0, 1000, "fa", "متن")]),
            Metadata("transcription", mode),
            correlationId);

    private static AiMinutesResult ValidMinutes(
        ProcessingMode mode = ProcessingMode.Fast,
        string correlationId = "corr-1",
        string evidenceId = "seg-0001") => new(
            new AiCleanedTranscript(1,
                [new AiCleanedSegment("clean-0001", "متن", ["seg-0001"])]),
            new AiGeneratedMinutes(
                1, null, null, "خلاصه", [], [],
                [new AiDecision("تصمیم", [evidenceId])], [], [], []),
            Metadata("minutes", mode),
            correlationId);

    private static AiStageMetadata Metadata(
        string stage,
        ProcessingMode mode,
        string actualProvider = "gemini",
        string? actualModel = null,
        bool fallbackUsed = false) => new(
        stage,
        mode,
        "gemini",
        mode == ProcessingMode.Fast
            ? "gemini-3.5-flash-lite"
            : "gemini-3.6-flash",
        actualProvider,
        actualModel ?? (mode == ProcessingMode.Fast
            ? "gemini-3.5-flash-lite"
            : "gemini-3.6-flash"),
        fallbackUsed,
        fallbackUsed ? "FALLBACK" : null,
        "v1",
        1,
        Now,
        Now,
        0);

    private sealed record TestState(
        MeetingMediaService Service,
        RecordingUnitOfWork UnitOfWork,
        FakeStorage Storage,
        FakeAiClient Ai);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRepository(Meeting meeting) : IMeetingRepository
    {
        public Task<Meeting?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Meeting?>(id == meeting.Id ? meeting : null);
        public Task<IReadOnlyList<Meeting>> ListAsync(int skip, int take,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task AddAsync(Meeting value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public void Remove(Meeting value) => throw new NotSupportedException();
    }

    private sealed class RecordingUnitOfWork(Meeting meeting) : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public List<MeetingProcessingStatus> SavedStatuses { get; } = [];
        public Exception? Failure { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            SavedStatuses.Add(meeting.Status);
            if (Failure is not null)
            {
                throw Failure;
            }
            return Task.FromResult(1);
        }
    }

    private sealed class FakeStorage : IAudioStorage
    {
        public int SaveCount { get; private set; }
        public bool DeleteCalled { get; private set; }

        public Task<StoredAudio> SaveAsync(Stream source, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(new StoredAudio(
                "objects/00000000000000000000000000000000.wav",
                "audio/wav", "wav", source.Length, new string('a', 64)));
        }

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream("RIFF0000WAVE"u8.ToArray()));

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;
            return Task.CompletedTask;
        }

        public Task<IStagedAudioDeletion> StageDeleteAsync(
            string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAiClient : IAiServiceClient
    {
        public ProcessingMode Mode { get; set; } = ProcessingMode.Fast;
        public string CorrelationId { get; set; } = "corr-1";
        public AiServiceException? TranscriptionFailure { get; set; }
        public AiTranscriptionResult? TranscriptionResult { get; set; }
        public AiServiceException? MinutesFailure { get; set; }
        public AiMinutesResult? MinutesResult { get; set; }
        public Action? BeforeMinutes { get; set; }

        public Task<AiTranscriptionResult> TranscribeAsync(
            Stream audio, string fileName, string contentType, ProcessingMode mode,
            string correlationId, CancellationToken cancellationToken = default) =>
            TranscriptionFailure is null
                ? Task.FromResult(TranscriptionResult ?? ValidTranscription(Mode, CorrelationId))
                : Task.FromException<AiTranscriptionResult>(TranscriptionFailure);

        public Task<AiMinutesResult> GenerateMinutesAsync(
            AiRawTranscript rawTranscript, ProcessingMode mode, string correlationId,
            CancellationToken cancellationToken = default)
        {
            BeforeMinutes?.Invoke();
            if (MinutesFailure is not null)
            {
                return Task.FromException<AiMinutesResult>(MinutesFailure);
            }
            return Task.FromResult(
                MinutesResult ?? ValidMinutes(Mode, CorrelationId));
        }
    }
}
