using System.Buffers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MeetingMinutesAI.Application.Abstractions.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeetingMinutesAI.Infrastructure.Storage;

internal sealed class LocalAudioStorage : IAudioStorage
{
    private const int BufferSize = 1024 * 1024;
    private static readonly Regex StorageKeyPattern = new(
        "^objects/[0-9a-f]{32}\\.(wav|flac|ogg|aiff|aac|mp3|webm|wma|m4a)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _root;
    private readonly string _objects;
    private readonly string _temporary;
    private readonly string _trash;
    private readonly long _maximumBytes;
    private readonly ILogger<LocalAudioStorage> _logger;

    public LocalAudioStorage(
        IOptions<AudioStorageOptions> options,
        ILogger<LocalAudioStorage> logger)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        _objects = Path.Combine(_root, "objects");
        _temporary = Path.Combine(_root, ".tmp");
        _trash = Path.Combine(_root, ".trash");
        _maximumBytes = options.Value.MaxBytes;
        _logger = logger;
        CreatePrivateDirectory(_root);
        CreatePrivateDirectory(_objects);
        CreatePrivateDirectory(_temporary);
        CreatePrivateDirectory(_trash);
        PurgeDirectory(_temporary);
        PurgeDirectory(_trash);
    }

    public async Task<StoredAudio> SaveAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var temporaryPath = Path.Combine(_temporary, $"{Guid.NewGuid():N}.tmp");
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var header = new byte[64];
        var headerLength = 0;
        long total = 0;
        try
        {
            string sha256;
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                int read;
                while ((read = await content.ReadAsync(
                    buffer.AsMemory(0, buffer.Length),
                    cancellationToken)) > 0)
                {
                    total += read;
                    if (total > _maximumBytes)
                    {
                        throw new AudioUploadException(
                            AudioUploadFailureKind.TooLarge,
                            "audio_too_large",
                            "Audio file exceeds the configured size limit.");
                    }

                    if (headerLength < header.Length)
                    {
                        var copied = Math.Min(read, header.Length - headerLength);
                        buffer.AsSpan(0, copied).CopyTo(header.AsSpan(headerLength));
                        headerLength += copied;
                    }

                    hash.AppendData(buffer, 0, read);
                    await destination.WriteAsync(
                        buffer.AsMemory(0, read),
                        cancellationToken);
                }

                if (total == 0)
                {
                    throw new AudioUploadException(
                        AudioUploadFailureKind.Empty,
                        "empty_audio",
                        "Audio file is empty.");
                }

                await destination.FlushAsync(cancellationToken);
                sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            var detected = Detect(header.AsSpan(0, headerLength));
            var storageKey = $"objects/{Guid.NewGuid():N}.{detected.Extension}";
            var finalPath = ResolveKey(storageKey);
            SetPrivateFileMode(temporaryPath);
            File.Move(temporaryPath, finalPath);
            return new StoredAudio(
                storageKey,
                detected.ContentType,
                detected.Extension,
                total,
                sha256);
        }
        catch (AudioUploadException)
        {
            DeleteFile(temporaryPath);
            throw;
        }
        catch (OperationCanceledException)
        {
            DeleteFile(temporaryPath);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            DeleteFile(temporaryPath);
            throw new AudioStorageException("Audio could not be stored.", exception);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Stream stream = new FileStream(
                ResolveKey(storageKey),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Task.FromResult(stream);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AudioStorageException("Stored audio could not be opened.", exception);
        }
    }

    public Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            DeleteFile(ResolveKey(storageKey));
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            throw new AudioStorageException("Stored audio could not be deleted.", exception);
        }
    }

    public Task<IStagedAudioDeletion> StageDeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var originalPath = ResolveKey(storageKey);
            if (!File.Exists(originalPath))
            {
                return Task.FromResult<IStagedAudioDeletion>(
                    new StagedDeletion(null, null, _logger));
            }

            var stagedPath = Path.Combine(_trash, $"{Guid.NewGuid():N}.delete");
            File.Move(originalPath, stagedPath);
            return Task.FromResult<IStagedAudioDeletion>(
                new StagedDeletion(originalPath, stagedPath, _logger));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AudioStorageException("Stored audio could not be staged for deletion.", exception);
        }
    }

    private string ResolveKey(string storageKey)
    {
        if (!StorageKeyPattern.IsMatch(storageKey))
        {
            throw new AudioStorageException("Storage key is invalid.");
        }

        var relative = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(_root, relative));
        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new AudioStorageException("Storage key resolves outside the storage root.");
        }
        return resolved;
    }

    private static DetectedAudio Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 12
            && header[..4].SequenceEqual("RIFF"u8)
            && header[8..12].SequenceEqual("WAVE"u8))
        {
            return new("wav", "audio/wav");
        }
        if (header.Length >= 4 && header[..4].SequenceEqual("fLaC"u8))
        {
            return new("flac", "audio/flac");
        }
        if (header.Length >= 4 && header[..4].SequenceEqual("OggS"u8))
        {
            return new("ogg", "audio/ogg");
        }
        if (header.Length >= 12
            && header[..4].SequenceEqual("FORM"u8)
            && (header[8..12].SequenceEqual("AIFF"u8)
                || header[8..12].SequenceEqual("AIFC"u8)))
        {
            return new("aiff", "audio/aiff");
        }
        if (header.Length >= 2
            && header[0] == 0xFF
            && (header[1] & 0xF6) is 0xF0 or 0xF2)
        {
            return new("aac", "audio/aac");
        }
        if (header.Length >= 3 && header[..3].SequenceEqual("ID3"u8)
            || header.Length >= 2
            && header[0] == 0xFF
            && (header[1] & 0xE0) == 0xE0)
        {
            return new("mp3", "audio/mp3");
        }
        if (header.Length >= 4
            && header[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
        {
            return new("webm", "audio/webm");
        }
        if (header.Length >= 16
            && header[..16].SequenceEqual(
                Convert.FromHexString("3026B2758E66CF11A6D900AA0062CE6C")))
        {
            return new("wma", "audio/x-ms-wma");
        }
        if (header.Length >= 12 && ContainsFtyp(header[4..Math.Min(32, header.Length)]))
        {
            return new("m4a", "audio/mp4");
        }

        throw new AudioUploadException(
            AudioUploadFailureKind.Unsupported,
            "unsupported_audio",
            "Audio content is not a supported container.");
    }

    private static bool ContainsFtyp(ReadOnlySpan<byte> value)
    {
        for (var index = 0; index <= value.Length - 4; index++)
        {
            if (value.Slice(index, 4).SequenceEqual("ftyp"u8))
            {
                return true;
            }
        }
        return false;
    }

    private void PurgeDirectory(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "A stale private audio-storage file could not be removed.");
            }
        }
    }

    private static void CreatePrivateDirectory(string path)
    {
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static void SetPrivateFileMode(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed record DetectedAudio(string Extension, string ContentType);

    private sealed class StagedDeletion(
        string? originalPath,
        string? stagedPath,
        ILogger logger) : IStagedAudioDeletion
    {
        private bool _committed;

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _committed = true;
            if (stagedPath is not null)
            {
                try
                {
                    DeleteFile(stagedPath);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "A staged private audio file will be removed during storage cleanup.");
                }
            }
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_committed
                && originalPath is not null
                && stagedPath is not null
                && File.Exists(stagedPath)
                && !File.Exists(originalPath))
            {
                File.Move(stagedPath, originalPath);
            }
            return ValueTask.CompletedTask;
        }
    }
}
