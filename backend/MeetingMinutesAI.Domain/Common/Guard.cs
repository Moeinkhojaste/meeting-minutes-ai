namespace MeetingMinutesAI.Domain.Common;

internal static class Guard
{
    public static string Required(string value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maximumLength)
        {
            throw new DomainRuleException($"{fieldName} exceeds its maximum length.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Required(value, fieldName, maximumLength);
    }

    public static DateTimeOffset Utc(DateTimeOffset value) => value.ToUniversalTime();
}
