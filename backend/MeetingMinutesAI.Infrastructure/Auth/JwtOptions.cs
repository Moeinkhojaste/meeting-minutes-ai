namespace MeetingMinutesAI.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "development-secret-key-meeting-minutes-ai-must-be-at-least-32-chars-long";

    public string Issuer { get; set; } = "MeetingMinutesAI.Api";

    public string Audience { get; set; } = "MeetingMinutesAI.Client";

    public int ExpiryMinutes { get; set; } = 1440;
}
