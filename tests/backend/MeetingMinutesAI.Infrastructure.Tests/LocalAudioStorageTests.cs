using MeetingMinutesAI.Application.Abstractions.Storage;
using MeetingMinutesAI.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MeetingMinutesAI.Infrastructure.Tests;

public sealed class LocalAudioStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "MeetingMinutesAI.Tests",
        Guid.NewGuid().ToString("N"));

    [Theory]
    [MemberData(nameof(SupportedHeaders))]
    public async Task SaveAcceptsSupportedSignatures(
        byte[] header,
        string expectedExtension,
        string expectedContentType)
    {
        var storage = CreateStorage();

        var stored = await storage.SaveAsync(new MemoryStream(header));

        Assert.Matches(
            $"^objects/[0-9a-f]{{32}}\\.{expectedExtension}$",
            stored.StorageKey);
        Assert.Equal(expectedContentType, stored.ContentType);
        Assert.Equal(header.Length, stored.ByteLength);
        Assert.Equal(64, stored.Sha256.Length);
        await using var reopened = await storage.OpenReadAsync(stored.StorageKey);
        using var copy = new MemoryStream();
        await reopened.CopyToAsync(copy);
        Assert.Equal(header, copy.ToArray());
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, ".tmp")));
    }

    [Fact]
    public async Task SaveRejectsEmptyUnsupportedAndOversizedContentAndCleansTemporaryFiles()
    {
        var storage = CreateStorage(maxBytes: 12);

        var empty = await Assert.ThrowsAsync<AudioUploadException>(() =>
            storage.SaveAsync(new MemoryStream()));
        var spoofed = await Assert.ThrowsAsync<AudioUploadException>(() =>
            storage.SaveAsync(new MemoryStream("fake.mp3"u8.ToArray())));
        var oversized = await Assert.ThrowsAsync<AudioUploadException>(() =>
            storage.SaveAsync(new MemoryStream(new byte[13])));

        Assert.Equal(AudioUploadFailureKind.Empty, empty.Kind);
        Assert.Equal(AudioUploadFailureKind.Unsupported, spoofed.Kind);
        Assert.Equal(AudioUploadFailureKind.TooLarge, oversized.Kind);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, ".tmp")));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, "objects")));
    }

    [Fact]
    public async Task StorageKeysRejectTraversal()
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<AudioStorageException>(() =>
            storage.OpenReadAsync("../outside.wav"));
        await Assert.ThrowsAsync<AudioStorageException>(() =>
            storage.DeleteIfExistsAsync("objects/../../outside.wav"));
    }

    [Fact]
    public async Task StagedDeletionRollsBackUnlessCommitted()
    {
        var storage = CreateStorage();
        var stored = await storage.SaveAsync(new MemoryStream(WavHeader()));

        await using (await storage.StageDeleteAsync(stored.StorageKey))
        {
            await Assert.ThrowsAsync<AudioStorageException>(() =>
                storage.OpenReadAsync(stored.StorageKey));
        }
        await using (var restored = await storage.OpenReadAsync(stored.StorageKey))
        {
            Assert.True(restored.Length > 0);
        }

        await using (var deletion = await storage.StageDeleteAsync(stored.StorageKey))
        {
            await deletion.CommitAsync();
        }
        await Assert.ThrowsAsync<AudioStorageException>(() =>
            storage.OpenReadAsync(stored.StorageKey));
    }

    [Fact]
    public void StartupPurgesOnlyStaleTemporaryAndTrashFiles()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".tmp"));
        Directory.CreateDirectory(Path.Combine(_root, ".trash"));
        Directory.CreateDirectory(Path.Combine(_root, "objects"));
        File.WriteAllText(Path.Combine(_root, ".tmp", "stale.tmp"), "x");
        File.WriteAllText(Path.Combine(_root, ".trash", "stale.delete"), "x");
        File.WriteAllText(Path.Combine(_root, "objects", "keep"), "x");

        _ = CreateStorage();

        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, ".tmp")));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_root, ".trash")));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "objects")));
    }

    public static TheoryData<byte[], string, string> SupportedHeaders() => new()
    {
        { WavHeader(), "wav", "audio/wav" },
        { "fLaC0000"u8.ToArray(), "flac", "audio/flac" },
        { "OggS0000"u8.ToArray(), "ogg", "audio/ogg" },
        { "FORM0000AIFF"u8.ToArray(), "aiff", "audio/aiff" },
        { [0xFF, 0xF0, 0, 0], "aac", "audio/aac" },
        { "ID30000"u8.ToArray(), "mp3", "audio/mp3" },
        { [0x1A, 0x45, 0xDF, 0xA3], "webm", "audio/webm" },
        { Convert.FromHexString("3026B2758E66CF11A6D900AA0062CE6C"), "wma", "audio/x-ms-wma" },
        { [0, 0, 0, 24, 102, 116, 121, 112, 77, 52, 65, 32], "m4a", "audio/mp4" },
    };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LocalAudioStorage CreateStorage(long maxBytes = 1024) =>
        new(
            Options.Create(new AudioStorageOptions
            {
                RootPath = _root,
                MaxBytes = maxBytes,
            }),
            NullLogger<LocalAudioStorage>.Instance);

    private static byte[] WavHeader() => "RIFF0000WAVEfmt "u8.ToArray();
}
