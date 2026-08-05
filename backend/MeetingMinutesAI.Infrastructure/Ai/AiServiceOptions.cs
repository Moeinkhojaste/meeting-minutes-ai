namespace MeetingMinutesAI.Infrastructure.Ai;

public sealed class AiServiceOptions
{
    public const string SectionName = "AIService";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8000";
    public int TimeoutSeconds { get; set; } = 7200;
    public long MaxResponseBytes { get; set; } = 64L * 1024 * 1024;
}
