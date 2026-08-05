namespace MeetingMinutesAI.Application.Abstractions.Storage;

public interface IAudioStorage
{
    Task<StoredAudio> SaveAsync(
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<IStagedAudioDeletion> StageDeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}

public interface IStagedAudioDeletion : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public sealed record StoredAudio(
    string StorageKey,
    string ContentType,
    string CanonicalExtension,
    long ByteLength,
    string Sha256
);

public enum AudioUploadFailureKind
{
    Empty,
    TooLarge,
    Unsupported,
}

public sealed class AudioUploadException : Exception
{
    public AudioUploadException(
        AudioUploadFailureKind kind,
        string code,
        string safeMessage)
        : base(safeMessage)
    {
        Kind = kind;
        Code = code;
        SafeMessage = safeMessage;
    }

    public AudioUploadFailureKind Kind { get; }
    public string Code { get; }
    public string SafeMessage { get; }
}

public sealed class AudioStorageException : Exception
{
    public AudioStorageException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
