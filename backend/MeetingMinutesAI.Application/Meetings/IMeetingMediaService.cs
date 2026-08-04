using MeetingMinutesAI.Domain.Meetings;

namespace MeetingMinutesAI.Application.Meetings;

public interface IMeetingMediaService
{
    Task<MeetingView> UploadAudioAsync(
        Guid meetingId,
        string? originalFileName,
        Stream content,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default);

    Task<MeetingView> ProcessAsync(
        Guid meetingId,
        ProcessingMode mode,
        byte[] expectedVersion,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<RawTranscriptView> GetRawTranscriptAsync(
        Guid meetingId,
        CancellationToken cancellationToken = default);

    Task<CleanedTranscriptView> GetCleanedTranscriptAsync(
        Guid meetingId,
        CancellationToken cancellationToken = default);

    Task<GeneratedMinutesView> GetGeneratedMinutesAsync(
        Guid meetingId,
        CancellationToken cancellationToken = default);
}
