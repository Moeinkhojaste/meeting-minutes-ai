using System.Diagnostics;
using System.Text.RegularExpressions;
using MeetingMinutesAI.Application.Abstractions.Ai;
using MeetingMinutesAI.Application.Abstractions.Persistence;
using MeetingMinutesAI.Application.Abstractions.Storage;
using MeetingMinutesAI.Domain.Common;
using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Meetings;

public sealed partial class MeetingMediaService(
    IMeetingRepository repository,
    IUnitOfWork unitOfWork,
    IAudioStorage audioStorage,
    IAiServiceClient aiServiceClient,
    TimeProvider timeProvider) : IMeetingMediaService
{
    public async Task<MeetingView> UploadAudioAsync(
        Guid meetingId,
        string? originalFileName,
        Stream content,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(meetingId, cancellationToken);
        EnsureVersion(meeting, expectedVersion);
        if (meeting.Status != MeetingProcessingStatus.Created
            || meeting.AudioFile is not null)
        {
            throw new MeetingCommandConflictException(
                "Audio can only be uploaded to a new meeting.");
        }

        var stored = await audioStorage.SaveAsync(content, cancellationToken);
        try
        {
            meeting.AttachAudio(
                SafeFileName(originalFileName, stored.CanonicalExtension),
                stored.StorageKey,
                stored.ContentType,
                stored.ByteLength,
                null,
                stored.Sha256,
                timeProvider.GetUtcNow());
            await SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await audioStorage.DeleteIfExistsAsync(
                stored.StorageKey,
                CancellationToken.None);
            throw;
        }

        return MeetingViewMapper.Map(meeting);
    }

    public async Task<MeetingView> ProcessAsync(
        Guid meetingId,
        ProcessingMode mode,
        byte[] expectedVersion,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(meetingId, cancellationToken);
        EnsureVersion(meeting, expectedVersion);
        if (meeting.Status is not (
            MeetingProcessingStatus.Uploaded
            or MeetingProcessingStatus.Completed
            or MeetingProcessingStatus.PartiallyCompleted
            or MeetingProcessingStatus.Failed)
            || meeting.AudioFile is null)
        {
            throw new MeetingCommandConflictException(
                "The meeting is not ready for processing.");
        }

        await using var audio = await audioStorage.OpenReadAsync(
            meeting.AudioFile.StorageKey,
            cancellationToken);

        meeting.Queue(timeProvider.GetUtcNow());
        await SaveChangesAsync(cancellationToken);
        var run = meeting.StartTranscription(
            mode,
            correlationId,
            timeProvider.GetUtcNow());
        await SaveChangesAsync(cancellationToken);
        var totalDuration = Stopwatch.StartNew();

        AiTranscriptionResult transcription;
        try
        {
            transcription = await aiServiceClient.TranscribeAsync(
                audio,
                meeting.AudioFile.OriginalFileName,
                meeting.AudioFile.ContentType,
                mode,
                correlationId,
                cancellationToken);
            ValidateTranscription(transcription, mode, correlationId);
            var rawSegments = transcription.RawTranscript.Segments
                .Select((segment, position) => RawTranscriptSegment.Create(
                    segment.Id,
                    position,
                    segment.Speaker,
                    segment.StartMilliseconds,
                    segment.EndMilliseconds,
                    segment.Language,
                    segment.Text))
                .ToList();
            AddStage(run, transcription.Metadata, ProcessingStageKind.Transcription);
            meeting.SetRawTranscript(
                run,
                transcription.RawTranscript.SchemaVersion,
                rawSegments,
                timeProvider.GetUtcNow());
            meeting.StartMinutesGeneration(run, timeProvider.GetUtcNow());
            await SaveChangesAsync(cancellationToken);
        }
        catch (AiServiceException failure)
        {
            await FailTranscriptionAsync(
                meeting,
                run,
                failure,
                totalDuration.ElapsedMilliseconds,
                cancellationToken);
            throw new MeetingProcessingException(failure);
        }
        catch (DomainRuleException exception)
        {
            var failure = InvalidAiResponse(exception);
            await FailTranscriptionAsync(
                meeting,
                run,
                failure,
                totalDuration.ElapsedMilliseconds,
                cancellationToken);
            throw new MeetingProcessingException(failure);
        }

        AiMinutesResult generated;
        try
        {
            generated = await aiServiceClient.GenerateMinutesAsync(
                transcription.RawTranscript,
                mode,
                correlationId,
                cancellationToken);
            var prepared = PrepareStageTwo(meeting, run, generated, mode, correlationId);
            meeting.SetCleanedTranscript(
                run,
                generated.CleanedTranscript.SchemaVersion,
                prepared.CleanedSegments,
                timeProvider.GetUtcNow());
            AddStage(run, generated.Metadata, ProcessingStageKind.Minutes);
            meeting.AttachGeneratedMinutes(
                run,
                prepared.Minutes,
                timeProvider.GetUtcNow());
            run.Complete(
                timeProvider.GetUtcNow(),
                totalDuration.ElapsedMilliseconds);
            meeting.Complete(run, timeProvider.GetUtcNow());
            await SaveChangesAsync(cancellationToken);
        }
        catch (AiServiceException failure)
        {
            return await CompletePartiallyAsync(
                meeting,
                run,
                failure,
                totalDuration.ElapsedMilliseconds,
                cancellationToken);
        }
        catch (DomainRuleException exception)
        {
            return await CompletePartiallyAsync(
                meeting,
                run,
                InvalidAiResponse(exception),
                totalDuration.ElapsedMilliseconds,
                cancellationToken);
        }

        return MeetingViewMapper.Map(meeting);
    }

    public async Task<RawTranscriptView> GetRawTranscriptAsync(
        Guid meetingId,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(meetingId, cancellationToken);
        var transcript = meeting.RawTranscript
            ?? throw new MeetingOutputNotFoundException("raw transcript");
        return new RawTranscriptView(
            transcript.SchemaVersion,
            transcript.Segments
                .OrderBy(segment => segment.Position)
                .Select(segment => new RawSegmentView(
                    segment.ExternalId,
                    segment.Speaker,
                    segment.StartMilliseconds,
                    segment.EndMilliseconds,
                    segment.Language,
                    segment.Text))
                .ToList(),
            transcript.CreatedAt,
            meeting.RowVersion.ToArray());
    }

    public async Task<CleanedTranscriptView> GetCleanedTranscriptAsync(
        Guid meetingId,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(meetingId, cancellationToken);
        var transcript = meeting.CleanedTranscript
            ?? throw new MeetingOutputNotFoundException("cleaned transcript");
        return new CleanedTranscriptView(
            transcript.SchemaVersion,
            transcript.Segments
                .OrderBy(segment => segment.Position)
                .Select(segment => new CleanedSegmentView(
                    segment.ExternalId,
                    segment.Text,
                    segment.Sources
                        .Select(source => source.RawSegment.ExternalId)
                        .ToList()))
                .ToList(),
            transcript.CreatedAt,
            meeting.RowVersion.ToArray());
    }

    public async Task<GeneratedMinutesView> GetGeneratedMinutesAsync(
        Guid meetingId,
        CancellationToken cancellationToken = default)
    {
        var meeting = await GetRequiredAsync(meetingId, cancellationToken);
        var minutes = meeting.Minutes.SingleOrDefault(
            item => item.Kind == MeetingMinutesKind.Generated)
            ?? throw new MeetingOutputNotFoundException("generated minutes");
        return MapMinutes(minutes, meeting.RowVersion);
    }

    private StageTwoOutput PrepareStageTwo(
        Meeting meeting,
        ProcessingRun run,
        AiMinutesResult result,
        ProcessingMode mode,
        string correlationId)
    {
        var rawById = meeting.RawTranscript!.Segments.ToDictionary(
            segment => segment.ExternalId,
            StringComparer.Ordinal);
        ValidateMinutes(
            result,
            mode,
            correlationId,
            rawById.Keys.ToHashSet(StringComparer.Ordinal));

        IReadOnlyList<RawTranscriptSegment> Evidence(IReadOnlyList<string> ids) =>
            ids.Select(id => rawById[id]).ToList();

        var cleaned = result.CleanedTranscript.Segments
            .Select((segment, position) => CleanedTranscriptSegment.Create(
                segment.Id,
                position,
                segment.Text,
                Evidence(segment.SourceRawSegmentIds)))
            .ToList();
        var source = result.Minutes;
        var minutes = MeetingMinutes.CreateGenerated(
            meeting.Id,
            run.Id,
            source.SchemaVersion,
            source.Title,
            source.Date,
            source.Summary,
            timeProvider.GetUtcNow());
        foreach (var item in source.Participants)
        {
            minutes.AddParticipant(item.Name, Evidence(item.EvidenceSegmentIds));
        }
        foreach (var item in source.Topics)
        {
            minutes.AddTopic(
                item.Title,
                item.Summary,
                Evidence(item.EvidenceSegmentIds));
        }
        foreach (var item in source.Decisions)
        {
            minutes.AddDecision(item.Text, Evidence(item.EvidenceSegmentIds));
        }
        foreach (var item in source.ActionItems)
        {
            minutes.AddActionItem(
                item.Task,
                item.Assignee,
                item.Deadline,
                Evidence(item.EvidenceSegmentIds));
        }
        foreach (var item in source.OpenQuestions)
        {
            minutes.AddOpenQuestion(item.Text, Evidence(item.EvidenceSegmentIds));
        }
        foreach (var item in source.Uncertainties)
        {
            minutes.AddUncertainty(
                item.Field,
                item.Description,
                Evidence(item.EvidenceSegmentIds));
        }

        return new StageTwoOutput(cleaned, minutes);
    }

    private static void ValidateTranscription(
        AiTranscriptionResult result,
        ProcessingMode mode,
        string correlationId)
    {
        ValidateStage(result.Metadata, "transcription", mode);
        if (result.RawTranscript.SchemaVersion != 1
            || !string.Equals(result.CorrelationId, correlationId, StringComparison.Ordinal))
        {
            throw new DomainRuleException("AI transcription metadata is invalid.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        long previousStart = -1;
        foreach (var segment in result.RawTranscript.Segments)
        {
            if (!RawSegmentId().IsMatch(segment.Id)
                || !ids.Add(segment.Id)
                || segment.StartMilliseconds < previousStart
                || segment.EndMilliseconds <= segment.StartMilliseconds)
            {
                throw new DomainRuleException("AI raw transcript is invalid.");
            }
            previousStart = segment.StartMilliseconds;
        }
    }

    private static void ValidateMinutes(
        AiMinutesResult result,
        ProcessingMode mode,
        string correlationId,
        IReadOnlySet<string> rawIds)
    {
        ValidateStage(result.Metadata, "minutes", mode);
        if (result.CleanedTranscript.SchemaVersion != 1
            || result.Minutes.SchemaVersion != 1
            || !string.Equals(result.CorrelationId, correlationId, StringComparison.Ordinal))
        {
            throw new DomainRuleException("AI minutes metadata is invalid.");
        }

        var cleanIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var segment in result.CleanedTranscript.Segments)
        {
            if (!CleanSegmentId().IsMatch(segment.Id)
                || !cleanIds.Add(segment.Id)
                || segment.SourceRawSegmentIds.Count == 0
                || segment.SourceRawSegmentIds.Any(id => !rawIds.Contains(id)))
            {
                throw new DomainRuleException("AI cleaned transcript is invalid.");
            }
        }

        var evidenceGroups = result.Minutes.Participants.Select(item => item.EvidenceSegmentIds)
            .Concat(result.Minutes.Topics.Select(item => item.EvidenceSegmentIds))
            .Concat(result.Minutes.Decisions.Select(item => item.EvidenceSegmentIds))
            .Concat(result.Minutes.ActionItems.Select(item => item.EvidenceSegmentIds))
            .Concat(result.Minutes.OpenQuestions.Select(item => item.EvidenceSegmentIds));
        if (evidenceGroups.Any(group => group.Count == 0)
            || evidenceGroups.SelectMany(group => group)
                .Concat(result.Minutes.Uncertainties.SelectMany(item => item.EvidenceSegmentIds))
                .Any(id => !rawIds.Contains(id)))
        {
            throw new DomainRuleException("AI minutes evidence is invalid.");
        }
    }

    private static void ValidateStage(
        AiStageMetadata metadata,
        string stage,
        ProcessingMode mode)
    {
        if (!string.Equals(metadata.Stage, stage, StringComparison.Ordinal)
            || metadata.RequestedMode != mode
            || !string.Equals(metadata.PrimaryProvider, "gemini", StringComparison.Ordinal)
            || !string.Equals(
                metadata.PrimaryModel,
                mode == ProcessingMode.Fast
                    ? "gemini-3.5-flash-lite"
                    : "gemini-3.6-flash",
                StringComparison.Ordinal)
            || metadata.ActualProvider is not ("gemini" or "faster-whisper")
            || (metadata.FallbackUsed
                != string.Equals(
                    metadata.ActualProvider,
                    "faster-whisper",
                    StringComparison.Ordinal))
            || metadata.SchemaVersion != 1
            || metadata.CompletedAt < metadata.StartedAt
            || metadata.DurationMilliseconds < 0)
        {
            throw new DomainRuleException("AI processing stage metadata is invalid.");
        }
    }

    private static void AddStage(
        ProcessingRun run,
        AiStageMetadata metadata,
        ProcessingStageKind stage) =>
        run.AddStage(
            stage,
            metadata.PrimaryProvider,
            metadata.PrimaryModel,
            metadata.ActualProvider,
            metadata.ActualModel,
            metadata.FallbackUsed,
            metadata.FallbackReason,
            metadata.PromptVersion,
            metadata.SchemaVersion,
            metadata.StartedAt,
            metadata.CompletedAt,
            metadata.DurationMilliseconds);

    private async Task FailTranscriptionAsync(
        Meeting meeting,
        ProcessingRun run,
        AiServiceException failure,
        long totalDurationMilliseconds,
        CancellationToken cancellationToken)
    {
        var failedAt = timeProvider.GetUtcNow();
        run.Fail(
            failure.Code,
            failure.SafeMessage,
            failure.Retryable,
            failedAt,
            totalDurationMilliseconds);
        meeting.Fail(run, failure.Code, failure.SafeMessage, failedAt);
        await SaveChangesAsync(cancellationToken);
    }

    private async Task<MeetingView> CompletePartiallyAsync(
        Meeting meeting,
        ProcessingRun run,
        AiServiceException failure,
        long totalDurationMilliseconds,
        CancellationToken cancellationToken)
    {
        var completedAt = timeProvider.GetUtcNow();
        run.CompletePartially(
            failure.Code,
            failure.SafeMessage,
            failure.Retryable,
            completedAt,
            totalDurationMilliseconds);
        meeting.CompletePartially(
            run,
            failure.Code,
            failure.SafeMessage,
            completedAt);
        await SaveChangesAsync(cancellationToken);
        return MeetingViewMapper.Map(meeting);
    }

    private async Task<Meeting> GetRequiredAsync(
        Guid meetingId,
        CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(meetingId, cancellationToken)
        ?? throw new MeetingNotFoundException();

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (PersistenceConcurrencyException)
        {
            throw new MeetingConcurrencyException();
        }
    }

    private static void EnsureVersion(Meeting meeting, byte[] expectedVersion)
    {
        if (!meeting.RowVersion.AsSpan().SequenceEqual(expectedVersion))
        {
            throw new MeetingConcurrencyException();
        }
    }

    private static AiServiceException InvalidAiResponse(Exception exception) =>
        new(
            AiFailureKind.InvalidResponse,
            "AI_INVALID_RESPONSE",
            "The AI service returned an invalid response.",
            false,
            exception);

    private static string SafeFileName(string? value, string extension)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? "audio" : value;
        candidate = candidate.Replace('\\', '/').Split('/').Last();
        candidate = new string(candidate
            .Where(character => !char.IsControl(character))
            .ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = "audio";
        }
        if (!candidate.Contains('.'))
        {
            candidate = $"{candidate}.{extension}";
        }
        return candidate.Length <= 255 ? candidate : candidate[..255];
    }

    private static GeneratedMinutesView MapMinutes(
        MeetingMinutes minutes,
        byte[] meetingVersion) =>
        new(
            minutes.SchemaVersion,
            minutes.Title,
            minutes.DateText,
            minutes.Summary,
            minutes.Participants.OrderBy(item => item.Position)
                .Select(item => new ParticipantView(
                    item.Name,
                    item.Evidence.Select(link => link.RawSegment.ExternalId).ToList()))
                .ToList(),
            minutes.Topics.OrderBy(item => item.Position)
                .Select(item => new TopicView(
                    item.Title,
                    item.Summary,
                    item.Evidence.Select(link => link.RawSegment.ExternalId).ToList()))
                .ToList(),
            minutes.Decisions.OrderBy(item => item.Position)
                .Select(item => new DecisionView(
                    item.Text,
                    item.Evidence.Select(link => link.RawSegment.ExternalId).ToList()))
                .ToList(),
            minutes.ActionItems.OrderBy(item => item.Position)
                .Select(item => new ActionItemView(
                    item.Task,
                    item.Assignee,
                    item.Deadline,
                    item.Evidence.Select(link => link.RawSegment.ExternalId).ToList()))
                .ToList(),
            minutes.OpenQuestions.OrderBy(item => item.Position)
                .Select(item => new OpenQuestionView(
                    item.Text,
                    item.Evidence.Select(link => link.RawSegment.ExternalId).ToList()))
                .ToList(),
            minutes.Uncertainties.OrderBy(item => item.Position)
                .Select(item => new UncertaintyView(
                    item.Field,
                    item.Description,
                    item.Evidence.Select(link => link.RawSegment.ExternalId).ToList()))
                .ToList(),
            minutes.CreatedAt,
            meetingVersion.ToArray());

    [GeneratedRegex("^seg-[0-9]{4,}$", RegexOptions.CultureInvariant)]
    private static partial Regex RawSegmentId();

    [GeneratedRegex("^clean-[0-9]{4,}$", RegexOptions.CultureInvariant)]
    private static partial Regex CleanSegmentId();

    private sealed record StageTwoOutput(
        IReadOnlyList<CleanedTranscriptSegment> CleanedSegments,
        MeetingMinutes Minutes);
}
