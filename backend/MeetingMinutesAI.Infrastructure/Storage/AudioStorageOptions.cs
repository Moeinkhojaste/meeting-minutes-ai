namespace MeetingMinutesAI.Infrastructure.Storage;

public sealed class AudioStorageOptions
{
    public const string SectionName = "AudioStorage";
    public const long DefaultMaxBytes = 500L * 1024 * 1024;

    public string RootPath { get; set; } = string.Empty;
    public long MaxBytes { get; set; } = DefaultMaxBytes;

    public static string DefaultRootPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeetingMinutesAI",
            "audio");
}
