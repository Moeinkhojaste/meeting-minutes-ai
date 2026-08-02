using MeetingMinutesAI.Domain.Common;

namespace MeetingMinutesAI.Domain.Meetings;

public sealed class AudioFile
{
    private AudioFile()
    {
    }

    internal AudioFile(
        Guid meetingId,
        string originalFileName,
        string storageKey,
        string contentType,
        long byteLength,
        long? durationMilliseconds,
        string sha256,
        DateTimeOffset uploadedAt)
    {
        if (byteLength <= 0)
        {
            throw new DomainRuleException("Audio byte length must be positive.");
        }

        if (durationMilliseconds is <= 0)
        {
            throw new DomainRuleException("Audio duration must be positive when supplied.");
        }

        Id = Guid.NewGuid();
        MeetingId = meetingId;
        OriginalFileName = Guard.Required(originalFileName, "Original filename", 255);
        StorageKey = Guard.Required(storageKey, "Storage key", 500);
        ContentType = Guard.Required(contentType, "Content type", 128);
        ByteLength = byteLength;
        DurationMilliseconds = durationMilliseconds;
        Sha256 = Guard.Required(sha256, "SHA-256", 64);
        UploadedAt = Guard.Utc(uploadedAt);
    }

    public Guid Id { get; private set; }

    public Guid MeetingId { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string StorageKey { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long ByteLength { get; private set; }

    public long? DurationMilliseconds { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; private set; }
}
